using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Infrastructure.Persistence;
namespace TourLab.Azure.Provisioning;

public sealed record AzurePrincipalPlan(string Database,Dictionary<string,Guid> Runtime,Dictionary<string,Guid> Migrators,Guid Receipts);
public static class AzureSecurityPlan
{
    public static void ValidateFile(string file,string workload)
    {
        var config=new ConfigurationManager();
        config.AddJsonFile(Path.GetFullPath(file),optional:false,reloadOnChange:false);
        _=StartupSecrets.Validate(config,false,workload);
        // Offline only: no credentials acquired, network, migrations or Azure mutation.
    }
    public static string Sql(AzurePrincipalPlan plan)
    {
        if(!Regex.IsMatch(plan.Database,"^tourlab_azure(?:_[a-zA-Z0-9]+)*$") || plan.Database.Length>100 ||
            !plan.Runtime.Keys.Order().SequenceEqual(SqlRules.Schemas.Order()) ||
            !plan.Migrators.Keys.Order().SequenceEqual(SqlRules.Schemas.Order()))
            throw new ArgumentException("Explicit lab database and all five schema identity maps required.");
        var ids=plan.Runtime.Values.Concat(plan.Migrators.Values).Append(plan.Receipts).ToArray();
        if(ids.Any(id=>id==Guid.Empty)||ids.Distinct().Count()!=11)throw new ArgumentException("Eleven distinct nonempty principal OBJECT IDs are required.");
        var sql=new StringBuilder($"""
            -- AZ-009 REVIEWABLE ARTIFACT ONLY. Never automatically execute.
            -- Connect directly to the authorized Azure SQL database, never master.
            -- Object IDs were explicitly supplied; SID/TYPE=E avoids directory lookup or server logins.
            SET XACT_ABORT ON;
            IF DB_NAME()<>N'{plan.Database}' OR SERVERPROPERTY('EngineEdition')<>5
                THROW 51000,'Unexpected Azure SQL lab target.',1;
            IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'TourLabAzure.Project' AND CONVERT(nvarchar(128),value)=N'TourLabAzure')
                THROW 51000,'Expected lab ownership marker required.',1;
            BEGIN TRANSACTION;
            REVOKE CONNECT FROM guest;
            """);
        foreach(var schema in SqlRules.Schemas) {
            var owner=$"tourlab_azure_{schema}_owner";
            sql.AppendLine($"""
                IF DATABASE_PRINCIPAL_ID(N'{owner}') IS NULL CREATE USER [{owner}] WITHOUT LOGIN;
                IF SCHEMA_ID(N'{schema}') IS NULL EXEC(N'CREATE SCHEMA [{schema}] AUTHORIZATION [{owner}]');
                IF (SELECT USER_NAME(principal_id) FROM sys.schemas WHERE name=N'{schema}')<>N'{owner}'
                    THROW 51000,'Unexpected schema owner.',1;
                IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_{schema}_runtime') IS NULL CREATE ROLE [tourlab_azure_{schema}_runtime] AUTHORIZATION dbo;
                """);
        }
        foreach(var schema in SqlRules.Schemas) {
            var user=$"tourlab_azure_{schema}";var migrator=user+"_migrator";var role=user+"_runtime";
            User(sql,user,plan.Runtime[schema],schema);User(sql,migrator,plan.Migrators[schema],schema);
            sql.AppendLine($"""
                IF EXISTS(SELECT 1 FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id=m.role_principal_id WHERE m.member_principal_id=DATABASE_PRINCIPAL_ID(N'{user}') AND r.name<>N'{role}')
                    THROW 51000,'Unexpected inherited runtime role.',1;
                IF EXISTS(SELECT 1 FROM sys.database_role_members WHERE member_principal_id=DATABASE_PRINCIPAL_ID(N'{migrator}'))
                    THROW 51000,'Unexpected inherited migrator role.',1;
                IF IS_ROLEMEMBER(N'{role}',N'{user}')<>1 ALTER ROLE [{role}] ADD MEMBER [{user}];
                GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[{schema}] TO [{role}];
                DENY CREATE TABLE,CREATE VIEW,CREATE PROCEDURE,CREATE FUNCTION,CREATE SCHEMA,ALTER ANY USER,ALTER ANY ROLE TO [{role}];
                DENY ALTER,TAKE OWNERSHIP ON SCHEMA::[{schema}] TO [{role}];
                REVOKE SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES ON SCHEMA::[{schema}] FROM public;
                GRANT CREATE TABLE TO [{migrator}];
                GRANT ALTER,REFERENCES,SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[{schema}] TO [{migrator}];
                IF OBJECT_ID(N'[{schema}].[__EFMigrationsHistory]',N'U') IS NOT NULL
                    DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::[{schema}].[__EFMigrationsHistory] TO [{role}];
                """);
            foreach(var other in SqlRules.Schemas.Where(x=>x!=schema))
                sql.AppendLine($"""
                    DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[{other}] TO [{role}];
                    DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[{other}] TO [{migrator}];
                    """);
        }
        User(sql,"tourlab_azure_receipts",plan.Receipts,"notifications");
        sql.AppendLine("""
            IF EXISTS(SELECT 1 FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id=m.role_principal_id WHERE m.member_principal_id=DATABASE_PRINCIPAL_ID(N'tourlab_azure_receipts') AND r.name<>N'tourlab_azure_notifications_runtime')
                THROW 51000,'Unexpected inherited receipt role.',1;
            IF IS_ROLEMEMBER(N'tourlab_azure_notifications_runtime',N'tourlab_azure_receipts')<>1
                ALTER ROLE [tourlab_azure_notifications_runtime] ADD MEMBER [tourlab_azure_receipts];
            COMMIT;
            -- Re-run after migrations to enforce history-object denials. No seed secrets, DDL cleanup or DROP.
            """);
        sql.Insert(sql.ToString().LastIndexOf("COMMIT;",StringComparison.Ordinal),ReceiptRestrictions);
        return sql.ToString();
    }
    public const string ReceiptRestrictions="""
        IF OBJECT_ID(N'notifications.ReceiptJobs',N'U') IS NOT NULL AND OBJECT_ID(N'notifications.ReceiptAttempts',N'U') IS NOT NULL
        BEGIN
            DENY EXECUTE,REFERENCES ON SCHEMA::[notifications] TO [tourlab_azure_receipts];
            DENY INSERT,DELETE ON OBJECT::[notifications].[ReceiptJobs] TO [tourlab_azure_receipts];
            DENY UPDATE ON OBJECT::[notifications].[ReceiptJobs] ([ReservationId],[SagaId],[UserId],[Kind],[SourceBusinessVersion],[SnapshotJson],[ContentHash],[BlobKey],[ConfirmedAtUtc]) TO [tourlab_azure_receipts];
            DENY SELECT,UPDATE,DELETE ON OBJECT::[notifications].[ReceiptAttempts] TO [tourlab_azure_receipts];
            DECLARE @receiptDenials nvarchar(max)=N'';
            SELECT @receiptDenials=@receiptDenials+N'DENY SELECT,INSERT,UPDATE,DELETE,REFERENCES ON OBJECT::[notifications].'+QUOTENAME(name)+N' TO [tourlab_azure_receipts];'
                FROM sys.tables WHERE schema_id=SCHEMA_ID(N'notifications') AND name NOT IN(N'ReceiptJobs',N'ReceiptAttempts');
            EXEC sys.sp_executesql @receiptDenials;
        END;

        """;
    private static void User(StringBuilder sql,string name,Guid id,string schema) {
        var sid="0x"+Convert.ToHexString(id.ToByteArray());
        sql.AppendLine($"""
            IF DATABASE_PRINCIPAL_ID(N'{name}') IS NULL CREATE USER [{name}] WITH SID={sid},TYPE=E;
            IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'{name}' AND type='E' AND sid={sid})
                THROW 51000,'Existing principal differs from the reviewed object ID.',1;
            ALTER USER [{name}] WITH DEFAULT_SCHEMA=[{schema}];
            GRANT CONNECT TO [{name}];
            """);
    }
}
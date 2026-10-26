using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Infrastructure.Persistence;
namespace TourLab.Azure.Provisioning;

// Privileged one-shot tool only. Never referenced by a runtime application.
public static class CloudMigration
{
    public static void ValidateTarget(string schema,string server,string database,Guid objectId)
    {
        SqlRules.Schema(schema);
        if(!Regex.IsMatch(server,"^tourlabazure-[a-z0-9]{4,6}-sql\\.database\\.windows\\.net$") ||
            database!="tourlab_azure_study" || objectId==Guid.Empty)
            throw new ArgumentException("Exact reviewed cloud migration target required.");
    }
    public static string[] Batches(string sql)
    {
        if(string.IsNullOrWhiteSpace(sql)||sql.Length>8*1024*1024)throw new ArgumentException("Bounded migration SQL required.");
        // Only EF-generated scripts with standalone GO separators; not a general sqlcmd interpreter.
        if(Regex.IsMatch(sql,@"(?im)^\s*(?:GO\s+\d+|:r\s|:connect\s|!!)") || sql.Contains("$(",StringComparison.Ordinal))
            throw new ArgumentException("sqlcmd directives/repetition are not supported.");
        return Regex.Split(sql,@"(?im)^\s*GO\s*\r?$").Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
    }
    public static async Task ExecuteBatchesAsync(SqlConnection connection,string sql,CancellationToken ct)
    {
        foreach(var batch in Batches(sql)){
            await using var command=new SqlCommand(batch,connection){CommandTimeout=120};
            await command.ExecuteNonQueryAsync(ct);
        }
    }
    public static async Task ApplyAsync(string schema,string server,string database,string file,Guid objectId,CancellationToken ct)
    {
        ValidateTarget(schema,server,database,objectId);
        var token=Environment.GetEnvironmentVariable("AZURELAB_SQL_ACCESS_TOKEN");
        if(string.IsNullOrWhiteSpace(token))throw new ArgumentException("Short-lived OIDC-derived SQL token required.");
        var sql=await File.ReadAllTextAsync(file,ct);
        _=Batches(sql);
        var settings=new SqlConnectionStringBuilder {
            DataSource="tcp:"+server+",1433",InitialCatalog=database,Encrypt=SqlConnectionEncryptOption.Strict,
            TrustServerCertificate=false,Pooling=false,ConnectTimeout=5
        };
        await using var connection=new SqlConnection(settings.ConnectionString){AccessToken=token};
        await connection.OpenAsync(ct);
        await using var guard=new SqlCommand("""
            SELECT CASE WHEN DB_NAME()=@database AND SERVERPROPERTY('EngineEdition')=5
                AND USER_NAME()=@principal
                AND EXISTS(SELECT 1 FROM sys.database_principals WHERE principal_id=USER_ID() AND type='E' AND sid=@sid)
                AND NOT EXISTS(SELECT 1 FROM sys.database_role_members WHERE member_principal_id=USER_ID())
                AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'TourLabAzure.Project' AND CONVERT(nvarchar(128),value)=N'TourLabAzure')
                THEN 1 ELSE 0 END
            """,connection){CommandTimeout=5};
        guard.Parameters.AddWithValue("@database",database);
        guard.Parameters.AddWithValue("@principal","tourlab_azure_"+schema+"_migrator");
        guard.Parameters.Add("@sid",System.Data.SqlDbType.VarBinary,16).Value=objectId.ToByteArray();
        if(Convert.ToInt32(await guard.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Migration principal/ownership guard failed.");
        await ExecuteBatchesAsync(connection,sql,ct);
        if(schema=="identity") {
            var seedFile=Environment.GetEnvironmentVariable("AZURELAB_CLOUD_SEED_FILE")??throw new ArgumentException("Private synthetic-user seed file required.");
            var users=System.Text.Json.JsonSerializer.Deserialize<SeedUser[]>(await File.ReadAllTextAsync(seedFile,ct),new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new ArgumentException("Invalid seed file.");
            await DatabaseProvisioner.SeedUsersAsync(users,o=>o.UseSqlServer(connection,x=>x.MigrationsHistoryTable("__EFMigrationsHistory","identity")),ct);
        }
        if(schema=="catalog") {
            await using var db=new TourLab.Azure.Catalog.Persistence.CatalogDb(new DbContextOptionsBuilder<TourLab.Azure.Catalog.Persistence.CatalogDb>().UseSqlServer(connection,x=>x.MigrationsHistoryTable("__EFMigrationsHistory","catalog")).Options);
            await TourLab.Azure.Catalog.Persistence.CatalogSeed.RunAsync(db,TimeProvider.System,ct);
        }
    }
}

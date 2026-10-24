-- AZ-002 REVIEWABLE ARTIFACT ONLY. DO NOT EXECUTE IN AZURE WITHOUT SEPARATE AUTHORIZATION.
-- Connect directly to the intended Azure SQL database as its authorized Entra provisioner.
-- Set the exact expected database via SQLCMD: -v LabDatabase=tourlab_azure
-- External user display names below must map to the intended runtime/migrator managed identities.
-- Re-run after migrations to apply migration-history object denials.
:on error exit
IF DB_NAME() <> N'$(LabDatabase)' OR DB_NAME() NOT LIKE N'tourlab[_]azure%'
    THROW 51000, 'Unexpected lab database; provisioning refused.', 1;
GO
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_identity_owner') IS NULL CREATE USER [tourlab_azure_identity_owner] WITHOUT LOGIN;
IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity] AUTHORIZATION [tourlab_azure_identity_owner]');
GO
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_catalog_owner') IS NULL CREATE USER [tourlab_azure_catalog_owner] WITHOUT LOGIN;
IF SCHEMA_ID(N'catalog') IS NULL EXEC(N'CREATE SCHEMA [catalog] AUTHORIZATION [tourlab_azure_catalog_owner]');
GO
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_reservations_owner') IS NULL CREATE USER [tourlab_azure_reservations_owner] WITHOUT LOGIN;
IF SCHEMA_ID(N'reservations') IS NULL EXEC(N'CREATE SCHEMA [reservations] AUTHORIZATION [tourlab_azure_reservations_owner]');
GO
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_payments_owner') IS NULL CREATE USER [tourlab_azure_payments_owner] WITHOUT LOGIN;
IF SCHEMA_ID(N'payments') IS NULL EXEC(N'CREATE SCHEMA [payments] AUTHORIZATION [tourlab_azure_payments_owner]');
GO
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_notifications_owner') IS NULL CREATE USER [tourlab_azure_notifications_owner] WITHOUT LOGIN;
IF SCHEMA_ID(N'notifications') IS NULL EXEC(N'CREATE SCHEMA [notifications] AUTHORIZATION [tourlab_azure_notifications_owner]');
GO
IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-identity') IS NULL CREATE USER [tourlab-azure-identity] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-identity-migrator') IS NULL CREATE USER [tourlab-azure-identity-migrator] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_identity_runtime') IS NULL CREATE ROLE [tourlab_azure_identity_runtime] AUTHORIZATION dbo;
IF IS_ROLEMEMBER(N'tourlab_azure_identity_runtime',N'tourlab-azure-identity')<>1 ALTER ROLE [tourlab_azure_identity_runtime] ADD MEMBER [tourlab-azure-identity];
GRANT CONNECT TO [tourlab-azure-identity];
GRANT CONNECT TO [tourlab-azure-identity-migrator];
GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[identity] TO [tourlab_azure_identity_runtime];
GRANT CREATE TABLE TO [tourlab-azure-identity-migrator];
GRANT ALTER,REFERENCES,SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[identity] TO [tourlab-azure-identity-migrator];
DENY CREATE TABLE,CREATE VIEW,CREATE PROCEDURE,CREATE FUNCTION,CREATE SCHEMA,ALTER ANY USER,ALTER ANY ROLE TO [tourlab_azure_identity_runtime];
DENY ALTER,TAKE OWNERSHIP ON SCHEMA::[identity] TO [tourlab_azure_identity_runtime];
REVOKE SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES ON SCHEMA::[identity] FROM public;
IF OBJECT_ID(N'[identity].[__EFMigrationsHistory]',N'U') IS NOT NULL
    DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::[identity].[__EFMigrationsHistory] TO [tourlab_azure_identity_runtime];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[catalog] TO [tourlab_azure_identity_runtime];
DENY ALTER ON SCHEMA::[catalog] TO [tourlab-azure-identity-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[reservations] TO [tourlab_azure_identity_runtime];
DENY ALTER ON SCHEMA::[reservations] TO [tourlab-azure-identity-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[payments] TO [tourlab_azure_identity_runtime];
DENY ALTER ON SCHEMA::[payments] TO [tourlab-azure-identity-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[notifications] TO [tourlab_azure_identity_runtime];
DENY ALTER ON SCHEMA::[notifications] TO [tourlab-azure-identity-migrator];
GO

IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-catalog') IS NULL CREATE USER [tourlab-azure-catalog] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-catalog-migrator') IS NULL CREATE USER [tourlab-azure-catalog-migrator] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_catalog_runtime') IS NULL CREATE ROLE [tourlab_azure_catalog_runtime] AUTHORIZATION dbo;
IF IS_ROLEMEMBER(N'tourlab_azure_catalog_runtime',N'tourlab-azure-catalog')<>1 ALTER ROLE [tourlab_azure_catalog_runtime] ADD MEMBER [tourlab-azure-catalog];
GRANT CONNECT TO [tourlab-azure-catalog];
GRANT CONNECT TO [tourlab-azure-catalog-migrator];
GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[catalog] TO [tourlab_azure_catalog_runtime];
GRANT CREATE TABLE TO [tourlab-azure-catalog-migrator];
GRANT ALTER,REFERENCES,SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[catalog] TO [tourlab-azure-catalog-migrator];
DENY CREATE TABLE,CREATE VIEW,CREATE PROCEDURE,CREATE FUNCTION,CREATE SCHEMA,ALTER ANY USER,ALTER ANY ROLE TO [tourlab_azure_catalog_runtime];
DENY ALTER,TAKE OWNERSHIP ON SCHEMA::[catalog] TO [tourlab_azure_catalog_runtime];
REVOKE SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES ON SCHEMA::[catalog] FROM public;
IF OBJECT_ID(N'[catalog].[__EFMigrationsHistory]',N'U') IS NOT NULL
    DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::[catalog].[__EFMigrationsHistory] TO [tourlab_azure_catalog_runtime];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[identity] TO [tourlab_azure_catalog_runtime];
DENY ALTER ON SCHEMA::[identity] TO [tourlab-azure-catalog-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[reservations] TO [tourlab_azure_catalog_runtime];
DENY ALTER ON SCHEMA::[reservations] TO [tourlab-azure-catalog-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[payments] TO [tourlab_azure_catalog_runtime];
DENY ALTER ON SCHEMA::[payments] TO [tourlab-azure-catalog-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[notifications] TO [tourlab_azure_catalog_runtime];
DENY ALTER ON SCHEMA::[notifications] TO [tourlab-azure-catalog-migrator];
GO

IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-reservations') IS NULL CREATE USER [tourlab-azure-reservations] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-reservations-migrator') IS NULL CREATE USER [tourlab-azure-reservations-migrator] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_reservations_runtime') IS NULL CREATE ROLE [tourlab_azure_reservations_runtime] AUTHORIZATION dbo;
IF IS_ROLEMEMBER(N'tourlab_azure_reservations_runtime',N'tourlab-azure-reservations')<>1 ALTER ROLE [tourlab_azure_reservations_runtime] ADD MEMBER [tourlab-azure-reservations];
GRANT CONNECT TO [tourlab-azure-reservations];
GRANT CONNECT TO [tourlab-azure-reservations-migrator];
GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[reservations] TO [tourlab_azure_reservations_runtime];
GRANT CREATE TABLE TO [tourlab-azure-reservations-migrator];
GRANT ALTER,REFERENCES,SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[reservations] TO [tourlab-azure-reservations-migrator];
DENY CREATE TABLE,CREATE VIEW,CREATE PROCEDURE,CREATE FUNCTION,CREATE SCHEMA,ALTER ANY USER,ALTER ANY ROLE TO [tourlab_azure_reservations_runtime];
DENY ALTER,TAKE OWNERSHIP ON SCHEMA::[reservations] TO [tourlab_azure_reservations_runtime];
REVOKE SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES ON SCHEMA::[reservations] FROM public;
IF OBJECT_ID(N'[reservations].[__EFMigrationsHistory]',N'U') IS NOT NULL
    DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::[reservations].[__EFMigrationsHistory] TO [tourlab_azure_reservations_runtime];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[identity] TO [tourlab_azure_reservations_runtime];
DENY ALTER ON SCHEMA::[identity] TO [tourlab-azure-reservations-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[catalog] TO [tourlab_azure_reservations_runtime];
DENY ALTER ON SCHEMA::[catalog] TO [tourlab-azure-reservations-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[payments] TO [tourlab_azure_reservations_runtime];
DENY ALTER ON SCHEMA::[payments] TO [tourlab-azure-reservations-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[notifications] TO [tourlab_azure_reservations_runtime];
DENY ALTER ON SCHEMA::[notifications] TO [tourlab-azure-reservations-migrator];
GO

IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-payments') IS NULL CREATE USER [tourlab-azure-payments] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-payments-migrator') IS NULL CREATE USER [tourlab-azure-payments-migrator] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_payments_runtime') IS NULL CREATE ROLE [tourlab_azure_payments_runtime] AUTHORIZATION dbo;
IF IS_ROLEMEMBER(N'tourlab_azure_payments_runtime',N'tourlab-azure-payments')<>1 ALTER ROLE [tourlab_azure_payments_runtime] ADD MEMBER [tourlab-azure-payments];
GRANT CONNECT TO [tourlab-azure-payments];
GRANT CONNECT TO [tourlab-azure-payments-migrator];
GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[payments] TO [tourlab_azure_payments_runtime];
GRANT CREATE TABLE TO [tourlab-azure-payments-migrator];
GRANT ALTER,REFERENCES,SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[payments] TO [tourlab-azure-payments-migrator];
DENY CREATE TABLE,CREATE VIEW,CREATE PROCEDURE,CREATE FUNCTION,CREATE SCHEMA,ALTER ANY USER,ALTER ANY ROLE TO [tourlab_azure_payments_runtime];
DENY ALTER,TAKE OWNERSHIP ON SCHEMA::[payments] TO [tourlab_azure_payments_runtime];
REVOKE SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES ON SCHEMA::[payments] FROM public;
IF OBJECT_ID(N'[payments].[__EFMigrationsHistory]',N'U') IS NOT NULL
    DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::[payments].[__EFMigrationsHistory] TO [tourlab_azure_payments_runtime];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[identity] TO [tourlab_azure_payments_runtime];
DENY ALTER ON SCHEMA::[identity] TO [tourlab-azure-payments-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[catalog] TO [tourlab_azure_payments_runtime];
DENY ALTER ON SCHEMA::[catalog] TO [tourlab-azure-payments-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[reservations] TO [tourlab_azure_payments_runtime];
DENY ALTER ON SCHEMA::[reservations] TO [tourlab-azure-payments-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[notifications] TO [tourlab_azure_payments_runtime];
DENY ALTER ON SCHEMA::[notifications] TO [tourlab-azure-payments-migrator];
GO

IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-notifications') IS NULL CREATE USER [tourlab-azure-notifications] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab-azure-notifications-migrator') IS NULL CREATE USER [tourlab-azure-notifications-migrator] FROM EXTERNAL PROVIDER;
IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_notifications_runtime') IS NULL CREATE ROLE [tourlab_azure_notifications_runtime] AUTHORIZATION dbo;
IF IS_ROLEMEMBER(N'tourlab_azure_notifications_runtime',N'tourlab-azure-notifications')<>1 ALTER ROLE [tourlab_azure_notifications_runtime] ADD MEMBER [tourlab-azure-notifications];
GRANT CONNECT TO [tourlab-azure-notifications];
GRANT CONNECT TO [tourlab-azure-notifications-migrator];
GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[notifications] TO [tourlab_azure_notifications_runtime];
GRANT CREATE TABLE TO [tourlab-azure-notifications-migrator];
GRANT ALTER,REFERENCES,SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[notifications] TO [tourlab-azure-notifications-migrator];
DENY CREATE TABLE,CREATE VIEW,CREATE PROCEDURE,CREATE FUNCTION,CREATE SCHEMA,ALTER ANY USER,ALTER ANY ROLE TO [tourlab_azure_notifications_runtime];
DENY ALTER,TAKE OWNERSHIP ON SCHEMA::[notifications] TO [tourlab_azure_notifications_runtime];
REVOKE SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES ON SCHEMA::[notifications] FROM public;
IF OBJECT_ID(N'[notifications].[__EFMigrationsHistory]',N'U') IS NOT NULL
    DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::[notifications].[__EFMigrationsHistory] TO [tourlab_azure_notifications_runtime];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[identity] TO [tourlab_azure_notifications_runtime];
DENY ALTER ON SCHEMA::[identity] TO [tourlab-azure-notifications-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[catalog] TO [tourlab_azure_notifications_runtime];
DENY ALTER ON SCHEMA::[catalog] TO [tourlab-azure-notifications-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[reservations] TO [tourlab_azure_notifications_runtime];
DENY ALTER ON SCHEMA::[reservations] TO [tourlab-azure-notifications-migrator];
DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[payments] TO [tourlab_azure_notifications_runtime];
DENY ALTER ON SCHEMA::[payments] TO [tourlab-azure-notifications-migrator];
GO


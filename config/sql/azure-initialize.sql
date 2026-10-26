-- AZ-012 only: reviewed Entra SQL provisioner, direct connection to the exact tagged lab database.
-- Do not run this with a runtime/migrator identity. No server logins, master or application data copied.
SET XACT_ABORT ON;
IF DB_NAME()<>N'tourlab_azure_study' OR SERVERPROPERTY('EngineEdition')<>5
    THROW 51000,'Unexpected Azure SQL target.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) NOT LIKE N'tourlabazure-%-sql%'
    THROW 51000,'Unexpected lab SQL server.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'TourLabAzure.Project')
    EXEC sys.sp_addextendedproperty @name=N'TourLabAzure.Project',@value=N'TourLabAzure';
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'TourLabAzure.Project' AND CONVERT(nvarchar(128),value)=N'TourLabAzure')
    THROW 51000,'Existing ownership marker differs.',1;
ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON;

using Microsoft.Data.SqlClient;
using TourLab.Azure.Provisioning;

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => { args.Cancel = true; shutdown.Cancel(); };
try
{
    if (args.Length == 5 && args[0] == "initialize")
    {
        if (args[4] != "local-only") throw new ArgumentException("Local-only guard missing.");
        PrivateConfiguration.Initialize(Directory.GetCurrentDirectory(), args[1], args[2], args[3]);

        Console.WriteLine("Fresh Azure-only credentials and signing/Data Protection keys generated privately.");
    }
    else if (args.Length == 2 && args[0] == "bootstrap")
    {
        await DatabaseProvisioner.BootstrapAsync(PrivateConfiguration.Read(args[1]), shutdown.Token);
        Console.WriteLine("Owned SQL schemas, restricted principals, migrations and synthetic users provisioned.");
    }
    else throw new ArgumentException("Use initialize <output> <database> <LocalDb|Docker> local-only or bootstrap <private-config-path>.");
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Provisioning cancelled."); return 2; }
catch (Exception error)
{
    Console.Error.WriteLine(error is SqlException sql ? $"Provisioning failed: SQL error {sql.Number}, state {sql.State}, line {sql.LineNumber}." : $"Provisioning failed: {error.GetType().Name}.");
    return 1;
}

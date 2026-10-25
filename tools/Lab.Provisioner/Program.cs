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
    else if(args.Length==3 && args[0]=="validate-cloud")
    {
        AzureSecurityPlan.ValidateFile(args[2],args[1]);
        Console.WriteLine("Offline structural configuration PASS; cloud identity/permissions NOT VERIFIED. No network or mutation.");
    }
    else if(args.Length==3 && args[0]=="azure-sql-plan")
    {
        var plan=System.Text.Json.JsonSerializer.Deserialize<AzurePrincipalPlan>(File.ReadAllText(args[1]),new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new ArgumentException("Invalid SQL plan.");
        var target=Path.GetFullPath(args[2]);
        var owned=Path.Combine(Directory.GetCurrentDirectory(),".local")+Path.DirectorySeparatorChar;
        if(!target.StartsWith(owned,StringComparison.OrdinalIgnoreCase)||File.Exists(target))throw new ArgumentException("New Azure .local output required; existing artifacts are preserved.");
        await File.WriteAllTextAsync(target,AzureSecurityPlan.Sql(plan),shutdown.Token);
        Console.WriteLine("Reviewable Azure SQL principal artifact generated only; not executed.");
    }
    else if (args.Length == 2 && args[0] == "receipts-local")
    {
        await LocalReceipts.InitializeAsync(Directory.GetCurrentDirectory(), args[1], shutdown.Token);
        Console.WriteLine("Private receipt container, Notifications runtime and Functions host storage configured.");
    }
    else if (args.Length == 2 && args[0] == "dashboard-local")
    {
        await LocalDashboard.InitializeAsync(Directory.GetCurrentDirectory(), args[1], shutdown.Token);
        Console.WriteLine("Owned Cosmos booking container initialized; isolated encrypted dashboard key ring configured.");
    }
    else if (args.Length == 2 && args[0] == "images-local")
    {
        await LocalImages.InitializeAsync(Directory.GetCurrentDirectory(), args[1], shutdown.Token);
        Console.WriteLine("Dedicated private Azurite container verified; Catalog profile enabled.");
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

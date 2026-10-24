using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TourLab.Azure.Gateway.Identity;

public sealed class LabUser : IdentityUser;

public sealed class IdentityDb(DbContextOptions<IdentityDb> options)
    : IdentityDbContext<LabUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("identity");
        model.Entity<DataProtectionKey>().ToTable("DataProtectionKeys", "identity");
    }
}

public sealed class IdentityDesignFactory : IDesignTimeDbContextFactory<IdentityDb>
{
    public IdentityDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<IdentityDb>()
        .UseSqlServer("Server=127.0.0.1,14339;Database=tourlab_azure_design;Integrated Security=True;Encrypt=True",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")).Options);
}

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourLab.Azure.Gateway.Identity;

namespace TourLab.Azure.Provisioning;

public sealed record ProvisioningCancellation(CancellationToken Token);

public sealed class SeedUserManager(
    IUserStore<LabUser> store, IOptions<IdentityOptions> options, IPasswordHasher<LabUser> hasher,
    IEnumerable<IUserValidator<LabUser>> validators, IEnumerable<IPasswordValidator<LabUser>> passwordValidators,
    ILookupNormalizer normalizer, IdentityErrorDescriber errors, IServiceProvider services,
    ILogger<UserManager<LabUser>> logger, ProvisioningCancellation operation)
    : UserManager<LabUser>(store, options, hasher, validators, passwordValidators, normalizer, errors, services, logger)
{
    protected override CancellationToken CancellationToken => operation.Token;
}

public sealed class SeedRoleManager(
    IRoleStore<IdentityRole> store, IEnumerable<IRoleValidator<IdentityRole>> validators,
    ILookupNormalizer normalizer, IdentityErrorDescriber errors, ILogger<RoleManager<IdentityRole>> logger,
    ProvisioningCancellation operation) : RoleManager<IdentityRole>(store, validators, normalizer, errors, logger)
{
    protected override CancellationToken CancellationToken => operation.Token;
}

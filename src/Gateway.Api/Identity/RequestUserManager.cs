using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TourLab.Azure.Gateway.Identity;

// UserManager's public methods do not take a token; its protected hook propagates HTTP cancellation to stores.
public sealed class RequestUserManager(
    IUserStore<LabUser> store, IOptions<IdentityOptions> options, IPasswordHasher<LabUser> hasher,
    IEnumerable<IUserValidator<LabUser>> validators, IEnumerable<IPasswordValidator<LabUser>> passwordValidators,
    ILookupNormalizer normalizer, IdentityErrorDescriber errors, IServiceProvider services,
    ILogger<UserManager<LabUser>> logger, IHttpContextAccessor accessor)
    : UserManager<LabUser>(store, options, hasher, validators, passwordValidators, normalizer, errors, services, logger)
{
    protected override CancellationToken CancellationToken => accessor.HttpContext?.RequestAborted ?? CancellationToken.None;
}

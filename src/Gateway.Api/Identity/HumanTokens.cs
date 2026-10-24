using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using TourLab.Azure.Infrastructure.Security;

namespace TourLab.Azure.Gateway.Identity;

public sealed class HumanTokens : IDisposable
{
    public static readonly string[] Services = ["catalog", "reservations", "payments", "notifications"];
    private readonly RSA rsa = RSA.Create();
    private readonly TimeProvider clock;
    public HumanTokens(IConfiguration config, TimeProvider clock)
    {
        this.clock = clock;
        rsa.ImportFromPem(File.ReadAllText(config["Jwt:PrivateKeyPath"] ?? throw new InvalidOperationException("Human signing key is required.")));
    }

    public string Issue(string userId, string name, string role, string service, TimeSpan? lifetime = null)
    {
        if (!Services.Contains(service, StringComparer.Ordinal) || role is not ("Tourist" or "Admin") ||
            string.IsNullOrWhiteSpace(userId) || userId.Length > 128 || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Invalid bounded token identity.");
        var duration = lifetime ?? TimeSpan.FromMinutes(2);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(15)) throw new ArgumentException("Invalid token lifetime.");
        var now = clock.GetUtcNow().UtcDateTime;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            BackendSecurity.Issuer, BackendSecurity.Audience(service),
            [new Claim("sub", userId), new Claim("name", name), new Claim("role", role), new Claim("jti", Guid.NewGuid().ToString())],
            now, now.Add(duration), new SigningCredentials(new RsaSecurityKey(rsa) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } }, SecurityAlgorithms.RsaSha256)));
    }
    public void Dispose() => rsa.Dispose();
}

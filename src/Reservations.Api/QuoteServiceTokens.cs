using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace TourLab.Azure.Reservations;

// Separate service credential. Never issued through HTTP or sent to the browser.
public sealed class QuoteServiceTokens : IDisposable
{
    private readonly RSA rsa = RSA.Create();
    private readonly TimeProvider clock;
    public QuoteServiceTokens(IConfiguration config, TimeProvider clock)
    {
        this.clock = clock;
        rsa.ImportFromPem(File.ReadAllText(config["Quote:PrivateKeyPath"] ?? throw new InvalidOperationException("Quote signing key is required.")));
    }

    public string Issue()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "tourlab-azure-reservations", "tourlab-azure-catalog-quotes",
            [new Claim("sub", "reservations"), new Claim("permission", "quote:create"), new Claim("jti", Guid.NewGuid().ToString())],
            now, now.AddMinutes(2), new SigningCredentials(new RsaSecurityKey(rsa) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } }, SecurityAlgorithms.RsaSha256)));
    }
    public void Dispose() => rsa.Dispose();
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace InspectNow.Tests.Integration;

internal static class TestTokens
{
    public const string Issuer = "https://inspectnow.test";
    private const string Audience = "inspectnow-integration";
    private static readonly SymmetricSecurityKey Key = new(RandomNumberGenerator.GetBytes(32));

    public static void Configure(IServiceCollection services)
    {
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = null;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = Key,
                ValidateIssuerSigningKey = true
            };
        });
    }

    public static string Issue(string subject) => new JwtSecurityTokenHandler().WriteToken(
        new JwtSecurityToken(Issuer, Audience, [new Claim("sub", subject)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)));
}

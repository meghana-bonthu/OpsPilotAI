using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Security;

public sealed class JwtTokenService(
    IConfiguration configuration,
    UserManager<ApplicationUser> userManager)
{
    public async Task<(string Token, DateTime ExpiresAtUtc)> CreateTokenAsync(
        ApplicationUser user)
    {
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt__Key is required.");

        var jwtIssuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "Jwt__Issuer is required.");

        var jwtAudience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "Jwt__Audience is required.");

        var expiresAtUtc = DateTime.UtcNow.AddHours(1);

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id),

            new(
                JwtRegisteredClaimNames.Email,
                user.Email ?? string.Empty),

            new(
                ClaimTypes.NameIdentifier,
                user.Id),

            new(
                ClaimTypes.Email,
                user.Email ?? string.Empty)
        };

        var roles = await userManager.GetRolesAsync(user);

        claims.AddRange(
            roles.Select(role =>
                new Claim(ClaimTypes.Role, role)));

        var signingCredentials =
            new SigningCredentials(
                new SymmetricSecurityKey(
                    Convert.FromBase64String(jwtKey)),
                SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        return (
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }
}
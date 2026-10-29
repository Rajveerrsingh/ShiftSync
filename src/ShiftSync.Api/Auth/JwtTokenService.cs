using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ShiftSync.Domain.Entities;

namespace ShiftSync.Api.Auth;

public class JwtTokenService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiresMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT signing key is missing.");
        _issuer = configuration["Jwt:Issuer"] ?? "ShiftSync";
        _audience = configuration["Jwt:Audience"] ?? "ShiftSync";
        _expiresMinutes = int.TryParse(configuration["Jwt:ExpiresMinutes"], out var minutes) ? minutes : 720;
    }

    public string Create(Employee employee)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, employee.Id.ToString()),
            new("email", employee.Email),
            new("name", employee.FullName),
            new("role", employee.Role.ToString()),
            new("store_id", employee.StoreId.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Bill_App.Models;
using Bill_App.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Bill_App.Services;

public class JwtService(IConfiguration configuration) : IJwtService
{
    private readonly IConfiguration _configuration = configuration;

    public string GenerateToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT__KEY"] ?? jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new("Sub", user.Sub.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT__ISSUER"] ?? jwtSettings["Issuer"],
            audience: _configuration["JWT__AUDIENCE"] ?? jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(double.Parse(_configuration["JWT__DURATION_IN_MINUTES"] ?? jwtSettings["DurationInMinutes"]!)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
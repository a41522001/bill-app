using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Bill_App.Models;
using Bill_App.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Bill_App.Services;

public class TokenService(IConfiguration config) : ITokenService
{
    private readonly string _secret = config["Jwt:Secret"]!;

    public string GenerateToken(ClaimsPrincipal principal)
    {
        throw new NotImplementedException();
    }

    public bool ShouldRefresh(string token)
    {
        throw new NotImplementedException();
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));

            var result = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero  // 不給緩衝時間，過期就是過期
            }, out _);

            return result;
        }
        catch
        {
            return null;  // 驗證失敗
        }
    }
}
using System.Security.Claims;

namespace Bill_App_API.Interfaces;

public interface ITokenService
{
    ClaimsPrincipal? ValidateAccessToken(string token);
    string GenerateAccessToken(string name, string email, Guid sub);
    Guid GenerateRefreshToken();
    bool ShouldRefresh(string token);
}
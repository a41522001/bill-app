using Bill_App.Models;
using System.Security.Claims;

namespace Bill_App.Services.Interfaces;

public interface ITokenService
{
    ClaimsPrincipal? ValidateAccessToken(string token);
    string GenerateAccessToken(User user);
    Guid GenerateRefreshToken();
    bool ShouldRefresh(string token);
}
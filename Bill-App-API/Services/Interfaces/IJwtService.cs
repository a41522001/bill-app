using Bill_App.Models;

namespace Bill_App.Services.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
}
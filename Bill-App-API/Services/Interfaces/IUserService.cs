using Bill_App_API.Dtos;
using Bill_App_API.Models;
namespace Bill_App_API.Interfaces;

public interface IUserService
{
    Task<bool> Signup(UserSignupRequest req);
    Task<UserLoginResponse> Login(UserLoginRequest req);
    Task<Guid> RotateRefreshToken(Guid userId, DateTime expireAt);
    Task<Guid?> GetUserId(Guid sub);
    Task Logout(string refreshToken);
    Task<bool> VerifyEmail(Guid token);
    Task<UserLoginResponse> GoogleLogin(string idToken);
    Task<UserProfileResponse> GetProfile(Guid userId);
    Task ResendVerifyEmail(string email);
    Task ForgetPassword(string email);
    Task ResetPassword(UserResetPasswordRequest req);
}

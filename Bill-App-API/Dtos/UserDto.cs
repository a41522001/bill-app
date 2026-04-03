namespace Bill_App_API.Dtos;

public record UserSignupRequest(
    string Name,
    string Email,
    string Password
);

public record UserLoginRequest(
    string Email,
    string Password
);

public record UserLoginResponse(
    string AccessToken,
    Guid RefreshToken
);

public record GoogleLoginRequest(string IdToken);

public record UserProfileResponse(
    string Name,
    string Email,
    int AuthProvider,
    bool IsEmailVerified
);

public record UserResendVerifyEmailRequest(
    string Email
);

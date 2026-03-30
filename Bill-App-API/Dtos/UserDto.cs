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
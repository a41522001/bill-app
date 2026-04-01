namespace Bill_App_Cache.Dtos;
public enum IsOldType
{
    No = 0,
    Yes = 1
};
public record UserSubHash(
    Guid UserId,
    string Email,
    string Name
);
public record RefreshTokenHash(
    Guid UserId,
    string Expire,
    string Email,
    Guid Sub,
    string Name,
    IsOldType IsOld 
);
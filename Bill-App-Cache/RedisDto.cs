using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    Guid Sub,
    string Name,
    IsOldType IsOld 
);
using System;
using System.Collections.Generic;
using System.Text;

namespace Bill_App_Cache.Dtos;

public record UserSubHash(
    Guid UserId,
    string Email,
    string Name
);
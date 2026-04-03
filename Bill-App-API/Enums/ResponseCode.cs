namespace Bill_App_API.Enums;

/// <summary>
/// API Response Code definitions.
/// See docs/response-codes.md for full documentation.
/// </summary>
public static class ResponseCodeEnum
{
    // General (0–999)
    public const int Success = 0;
    public const int Error = 1;

    // Auth (1001–1999)
    public const int EmailNotVerified = 1001;
    public const int AccountBoundToGoogle = 1002;
    public const int AccountBoundToLocal = 1003;

    // Category (2001–2999)

    // Transaction (3001–3999)
}

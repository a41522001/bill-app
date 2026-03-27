namespace Bill_App.Options;

public class JwtOptions
{
    public string Key { get; init; } = "";
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public int DurationInMinutes { get; init; } = 15;
}

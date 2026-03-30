namespace Bill_App_API.Options;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int DurationInMinutes { get; set; } = 15;
}

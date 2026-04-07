namespace Bill_App_API.Options;

public class LoginRateLimitOptions
{
    public int IpLimit { get; set; }
    public int EmailLimit { get; set; }
    public int TtlMinute { get; set; }
}

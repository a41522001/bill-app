namespace Bill_App_API.Options;

public class RefreshTokenOptions
{
    public int DurationInDays { get; set; } = 7;
    public int OldTokenGraceInSeconds { get; set; } = 15;
}

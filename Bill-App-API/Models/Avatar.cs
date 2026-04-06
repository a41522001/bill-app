namespace Bill_App_API.Models;

public class Avatar
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string OriginalUrl { get; set; }
    public required string ThumbUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign key
    public Guid UserId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}

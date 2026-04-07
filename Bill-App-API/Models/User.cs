using Bill_App_API.Enums;
namespace Bill_App_API.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? Password { get; set; }
    public AuthProviderEnum AuthProvider { get; set; } = AuthProviderEnum.Local;
    public bool IsEmailVerified { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid Sub { get; set; } = Guid.NewGuid();

    // Navigation
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public Avatar? Avatar { get; set; }
}

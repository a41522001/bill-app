using Bill_App_API.Enums;

namespace Bill_App_API.Models;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required TransactionTypeEnum Type { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Foreign key
    public Guid UserId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

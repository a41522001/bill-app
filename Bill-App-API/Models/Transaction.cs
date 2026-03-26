using Bill_App.Enums;

namespace Bill_App.Models;

public class Transaction
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public required decimal Amount { get; set; }
  public string? Note { get; set; }
  public required TransactionTypeEnum Type { get; set; }
  public required DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  
  // Foreign key
  public Guid UserId { get; set; }
  public Guid CategoryId { get; set; }
  
  // Navigation
  public User User { get; set; } = null!;
  public Category Category { get; set; } = null!;
}
namespace Bill_App_API.Models;

public class User
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public required string Name { get; set; }
  public required string Email { get; set; }
  public required string Password { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public Guid Sub { get; set; } = Guid.NewGuid();

  // Navigation
  public ICollection<Category> Categories { get; set; } = new List<Category>();
  public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
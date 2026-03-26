using Microsoft.EntityFrameworkCore;
using Bill_App.Models;
namespace Bill_App.Contexts;

public class BillDbContext : DbContext
{
  public BillDbContext(DbContextOptions<BillDbContext> options) : base(options) { }
  public DbSet<User> Users { get; set; }
  public DbSet<Category> Categories { get; set; }
  public DbSet<Transaction> Transactions { get; set; }
}
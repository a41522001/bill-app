using Microsoft.EntityFrameworkCore;
using Bill_App_API.Models;
namespace Bill_App_API.Contexts;

public class BillDbContext : DbContext
{
    public BillDbContext(DbContextOptions<BillDbContext> options) : base(options) { }
    public DbSet<User> Users { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Avatar> Avatars { get; set; }
}

using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

public class AppDbContext : DbContext
{
    public DbSet<User> User { get; set; }
    public DbSet<Message> Message { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
             optionsBuilder.UseNpgsql("Host=webchatpg.c7m2swsiky3r.us-east-2.rds.amazonaws.com;Port=5432;Username=postgres;Password=12345678;Database=api-db");
        }
    }
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string Password { get; set; }
}

public class Message
{
    public int Id { get; set; }
    public string From { get; set; } = string.Empty;
    public string Receive { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

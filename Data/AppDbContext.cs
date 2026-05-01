using Microsoft.EntityFrameworkCore;
using LoyalAnimal.Shared;

namespace LoyalAnimal.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // MEVCUT
    public DbSet<User> Users => Set<User>();
    public DbSet<Pet> Pets => Set<Pet>();

    // 🔥 EKLEMEN GEREKENLER
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Message> Messages => Set<Message>();
}
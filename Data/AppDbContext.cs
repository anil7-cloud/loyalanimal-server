using Microsoft.EntityFrameworkCore;
using LoyalAnimal.Shared; // HATA BURADAYDI: .Api.Models yerine .Shared olmalı

namespace LoyalAnimal.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Pet> Pets => Set<Pet>();
}
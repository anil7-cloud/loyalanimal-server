using LoyalAnimal.Server.Data;
using LoyalAnimal.Shared;
using Microsoft.EntityFrameworkCore;

namespace LoyalAnimal.Server.Services;

public class PetService
{
    private readonly AppDbContext _context;

    public PetService(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<Pet>> GetAllPetsAsync()
        => _context.Pets.AsNoTracking().ToListAsync();
}
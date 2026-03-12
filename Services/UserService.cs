using System.Collections.Generic;
using System.Threading.Tasks;
using LoyalAnimal.Server.Data;
using LoyalAnimal.Shared;
using Microsoft.EntityFrameworkCore;

namespace LoyalAnimal.Server.Services;

public class UserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<User>> GetAllUsersAsync()
    {
        return _context.Users
            .AsNoTracking()
            .ToListAsync();
    }
}
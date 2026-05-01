using Microsoft.EntityFrameworkCore;
using LoyalAnimal.Server.Data;
using LoyalAnimal.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PetService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/users", () =>
{
    return Results.Ok(new List<AppUserDto>
    {
        new() { Id = 1, DisplayName = "Ali", City = "Ankara", Age = 25, Gender = "Erkek", PhotoUrl = "", CreatedAt = DateTime.UtcNow },
        new() { Id = 2, DisplayName = "Ayşe", City = "İstanbul", Age = 24, Gender = "Kadın", PhotoUrl = "", CreatedAt = DateTime.UtcNow },
        new() { Id = 3, DisplayName = "Merve", City = "İzmir", Age = 26, Gender = "Kadın", PhotoUrl = "", CreatedAt = DateTime.UtcNow }
    });
});


app.MapGet("/reset-db", async (AppDbContext db) =>
{
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Messages");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Matches");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Swipes");

    return Results.Ok(new
    {
        success = true,
        message = "Swipes, matches and messages reset"
    });
});


app.MapPost("/users/register", (CreateUserRequest request) =>
{
    return Results.Ok(new AppUserDto
    {
        Id = Random.Shared.Next(100, 999999),
        DisplayName = request.DisplayName,
        City = request.City,
        Age = request.Age,
        Gender = request.Gender,
        PhotoUrl = "",
        CreatedAt = DateTime.UtcNow
    });
});

app.MapPost("/swipes", (SwipeRequest request) =>
{
    return Results.Ok(new SwipeResultDto
    {
        Matched = request.IsLike,
        UserAId = request.FromUserId,
        UserBId = request.ToUserId
    });
});

app.MapGet("/matches/{userId:int}", (int userId) =>
{
    return Results.Ok(new List<UserMatchDto>
    {
        new()
        {
            Id = 1,
            UserAId = userId,
            UserBId = 2,
            CreatedAt = DateTime.UtcNow
        }
    });
});

app.MapGet("/messages/list/{matchId:int}", (int matchId) =>
{
    return Results.Ok(new List<MessageDto>
    {
        new()
        {
            Id = 1,
            MatchId = matchId,
            FromUserId = 1,
            ToUserId = 2,
            Text = "Merhaba 👋",
            CreatedAt = DateTime.UtcNow
        }
    });
});

app.MapPost("/messages/send", (SendMessageRequest request) =>
{
    return Results.Ok(new MessageDto
    {
        Id = Random.Shared.Next(100, 999999),
        MatchId = request.MatchId,
        FromUserId = request.FromUserId,
        ToUserId = request.ToUserId,
        Text = request.Text,
        CreatedAt = DateTime.UtcNow
    });
});

app.Run();

public class AppUserDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string City { get; set; } = "";
    public int Age { get; set; }
    public string Gender { get; set; } = "";
    public string PhotoUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class CreateUserRequest
{
    public string DisplayName { get; set; } = "";
    public string City { get; set; } = "";
    public int Age { get; set; }
    public string Gender { get; set; } = "";
}

public class SwipeRequest
{
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    public bool IsLike { get; set; }
}

public class SwipeResultDto
{
    public bool Matched { get; set; }
    public int UserAId { get; set; }
    public int UserBId { get; set; }
}

public class UserMatchDto
{
    public int Id { get; set; }
    public int UserAId { get; set; }
    public int UserBId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MessageDto
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class SendMessageRequest
{
    public int MatchId { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    public string Text { get; set; } = "";
}

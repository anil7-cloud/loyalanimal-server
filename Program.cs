using Microsoft.EntityFrameworkCore;
using LoyalAnimal.Server.Data;
using LoyalAnimal.Shared;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5298";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=/tmp/loyalanimal.db"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("AllowAll");

app.MapGet("/", () => Results.Ok(new
{
    success = true,
    message = "LoyalAnimal API Running"
}));

app.MapPost("/users/register", async (CreateUserRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.DisplayName) ||
        string.IsNullOrWhiteSpace(req.City) ||
        string.IsNullOrWhiteSpace(req.Gender) ||
        req.Age <= 0)
    {
        return Results.BadRequest(new { message = "Geçersiz kullanıcı" });
    }

    var user = new User
    {
        Username = req.DisplayName.Trim(),
        City = req.City.Trim(),
        Age = req.Age,
        Gender = req.Gender.Trim(),
        CreatedAtUtc = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(ToDto(user));
});

app.MapGet("/users", async (AppDbContext db) =>
{
    var users = await db.Users
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => ToDto(x))
        .ToListAsync();

    return Results.Ok(users);
});

// Discover: daha önce like/dislike edilen kullanıcıları tekrar göstermez
app.MapGet("/users/discover/{userId:int}", async (int userId, AppDbContext db) =>
{
    if (userId <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı" });

    var swipedIds = await db.Swipes
        .AsNoTracking()
        .Where(x => x.FromUserId == userId)
        .Select(x => x.ToUserId)
        .ToListAsync();

    var users = await db.Users
        .AsNoTracking()
        .Where(x =>
            x.Id != userId &&
            !swipedIds.Contains(x.Id))
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => ToDto(x))
        .ToListAsync();

    return Results.Ok(users);
});

app.MapPost("/swipes", async (SwipeRequest req, AppDbContext db) =>
{
    if (req.FromUserId <= 0 || req.ToUserId <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı" });

    if (req.FromUserId == req.ToUserId)
        return Results.BadRequest(new { message = "Kendini beğenemezsin" });

    var fromExists = await db.Users.AnyAsync(x => x.Id == req.FromUserId);
    var toExists = await db.Users.AnyAsync(x => x.Id == req.ToUserId);

    if (!fromExists || !toExists)
        return Results.NotFound(new { message = "Kullanıcı bulunamadı" });

    var old = await db.Swipes.FirstOrDefaultAsync(x =>
        x.FromUserId == req.FromUserId &&
        x.ToUserId == req.ToUserId);

    if (old == null)
    {
        db.Swipes.Add(new Swipe
        {
            FromUserId = req.FromUserId,
            ToUserId = req.ToUserId,
            IsLike = req.IsLike,
            CreatedAtUtc = DateTime.UtcNow
        });
    }
    else
    {
        old.IsLike = req.IsLike;
        old.CreatedAtUtc = DateTime.UtcNow;
    }

    await db.SaveChangesAsync();

    if (!req.IsLike)
        return Results.Ok(new SwipeResponse { Matched = false });

    var mutual = await db.Swipes.AnyAsync(x =>
        x.FromUserId == req.ToUserId &&
        x.ToUserId == req.FromUserId &&
        x.IsLike);

    if (!mutual)
        return Results.Ok(new SwipeResponse { Matched = false });

    var u1 = Math.Min(req.FromUserId, req.ToUserId);
    var u2 = Math.Max(req.FromUserId, req.ToUserId);

    var matchExists = await db.Matches.AnyAsync(x =>
        x.User1Id == u1 &&
        x.User2Id == u2);

    if (!matchExists)
    {
        db.Matches.Add(new Match
        {
            User1Id = u1,
            User2Id = u2,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    return Results.Ok(new SwipeResponse { Matched = true });
});

app.MapGet("/matches/{userId:int}", async (int userId, AppDbContext db) =>
{
    if (userId <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı" });

    var matches = await db.Matches
        .AsNoTracking()
        .Where(x => x.User1Id == userId || x.User2Id == userId)
        .ToListAsync();

    var ids = matches
        .Select(x => x.User1Id == userId ? x.User2Id : x.User1Id)
        .ToList();

    var users = await db.Users
        .AsNoTracking()
        .Where(x => ids.Contains(x.Id))
        .Select(x => ToDto(x))
        .ToListAsync();

    return Results.Ok(users);
});

app.MapGet("/likes/me/{userId:int}", async (int userId, AppDbContext db) =>
{
    if (userId <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı" });

    var likedMe = await db.Swipes
        .AsNoTracking()
        .Where(x => x.ToUserId == userId && x.IsLike)
        .Select(x => x.FromUserId)
        .Distinct()
        .ToListAsync();

    var matched = await db.Matches
        .AsNoTracking()
        .Where(x => x.User1Id == userId || x.User2Id == userId)
        .Select(x => x.User1Id == userId ? x.User2Id : x.User1Id)
        .ToListAsync();

    var users = await db.Users
        .AsNoTracking()
        .Where(x => likedMe.Contains(x.Id) && !matched.Contains(x.Id))
        .Select(x => ToDto(x))
        .ToListAsync();

    return Results.Ok(users);
});

app.MapGet("/messages/{u1:int}/{u2:int}", async (int u1, int u2, AppDbContext db) =>
{
    if (u1 <= 0 || u2 <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı" });

    var a = Math.Min(u1, u2);
    var b = Math.Max(u1, u2);

    var matched = await db.Matches.AnyAsync(x =>
        x.User1Id == a &&
        x.User2Id == b);

    if (!matched)
        return Results.BadRequest(new { message = "Match yok" });

    var msgs = await db.Messages
        .AsNoTracking()
        .Where(x =>
            (x.SenderUserId == u1 && x.ReceiverUserId == u2) ||
            (x.SenderUserId == u2 && x.ReceiverUserId == u1))
        .OrderBy(x => x.CreatedAtUtc)
        .Select(x => new MessageDto
        {
            Id = x.Id,
            SenderUserId = x.SenderUserId,
            ReceiverUserId = x.ReceiverUserId,
            Text = x.Text,
            CreatedAt = x.CreatedAtUtc
        })
        .ToListAsync();

    return Results.Ok(msgs);
});

app.MapPost("/messages", async (SendMessageRequest req, AppDbContext db) =>
{
    if (req.SenderUserId <= 0 ||
        req.ReceiverUserId <= 0 ||
        string.IsNullOrWhiteSpace(req.Text))
    {
        return Results.BadRequest(new { message = "Geçersiz mesaj" });
    }

    var a = Math.Min(req.SenderUserId, req.ReceiverUserId);
    var b = Math.Max(req.SenderUserId, req.ReceiverUserId);

    var matched = await db.Matches.AnyAsync(x =>
        x.User1Id == a &&
        x.User2Id == b);

    if (!matched)
        return Results.BadRequest(new { message = "Match yok" });

    var msg = new Message
    {
        SenderUserId = req.SenderUserId,
        ReceiverUserId = req.ReceiverUserId,
        Text = req.Text.Trim(),
        CreatedAtUtc = DateTime.UtcNow
    };

    db.Messages.Add(msg);
    await db.SaveChangesAsync();

    return Results.Ok(new MessageDto
    {
        Id = msg.Id,
        SenderUserId = msg.SenderUserId,
        ReceiverUserId = msg.ReceiverUserId,
        Text = msg.Text,
        CreatedAt = msg.CreatedAtUtc
    });
});

app.Run();

static AppUserDto ToDto(User u) => new()
{
    Id = u.Id,
    DisplayName = u.Username,
    City = u.City,
    Age = u.Age,
    Gender = u.Gender,
    PhotoUrl = "",
    CreatedAt = u.CreatedAtUtc
};

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

public class SwipeResponse
{
    public bool Matched { get; set; }
}

public class SendMessageRequest
{
    public int SenderUserId { get; set; }
    public int ReceiverUserId { get; set; }
    public string Text { get; set; } = "";
}

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

public class MessageDto
{
    public int Id { get; set; }
    public int SenderUserId { get; set; }
    public int ReceiverUserId { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
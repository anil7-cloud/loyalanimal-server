using Microsoft.EntityFrameworkCore;
using LoyalAnimal.Server.Data;
using LoyalAnimal.Server.Services;
using LoyalAnimal.Shared;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5298";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=/tmp/loyalanimal.db"));

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PetService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("AllowAll");

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        success = true,
        message = "LoyalAnimal API Running"
    });
});

app.MapPost("/users/register", async (CreateUserRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.DisplayName) ||
        string.IsNullOrWhiteSpace(request.City) ||
        string.IsNullOrWhiteSpace(request.Gender) ||
        request.Age <= 0)
    {
        return Results.BadRequest(new { message = "Geçersiz kullanıcı bilgileri." });
    }

    var user = new User
    {
        Username = request.DisplayName.Trim(),
        Email = "",
        PasswordHash = "",
        City = request.City.Trim(),
        Age = request.Age,
        Gender = request.Gender.Trim(),
        CreatedAtUtc = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(new AppUserDto
    {
        Id = user.Id,
        DisplayName = user.Username,
        City = user.City,
        Age = user.Age,
        Gender = user.Gender,
        PhotoUrl = "",
        CreatedAt = user.CreatedAtUtc
    });
});

app.MapGet("/users", async (AppDbContext db) =>
{
    var users = await db.Users
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => new AppUserDto
        {
            Id = x.Id,
            DisplayName = x.Username,
            City = x.City,
            Age = x.Age,
            Gender = x.Gender,
            PhotoUrl = "",
            CreatedAt = x.CreatedAtUtc
        })
        .ToListAsync();

    return Results.Ok(users);
});

app.MapGet("/users/discover/{userId:int}", async (int userId, AppDbContext db) =>
{
    if (userId <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı." });

    var users = await db.Users
        .AsNoTracking()
        .Where(x => x.Id != userId)
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => new AppUserDto
        {
            Id = x.Id,
            DisplayName = x.Username,
            City = x.City,
            Age = x.Age,
            Gender = x.Gender,
            PhotoUrl = "",
            CreatedAt = x.CreatedAtUtc
        })
        .ToListAsync();

    return Results.Ok(users);
});

app.MapPost("/swipes", async (SwipeRequest request, AppDbContext db) =>
{
    if (request.FromUserId <= 0 || request.ToUserId <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı bilgileri." });

    if (request.FromUserId == request.ToUserId)
        return Results.BadRequest(new { message = "Kendini beğenemezsin." });

    var fromUserExists = await db.Users.AnyAsync(x => x.Id == request.FromUserId);
    var toUserExists = await db.Users.AnyAsync(x => x.Id == request.ToUserId);

    if (!fromUserExists || !toUserExists)
        return Results.NotFound(new { message = "Kullanıcı bulunamadı." });

    var oldSwipe = await db.Swipes.FirstOrDefaultAsync(x =>
        x.FromUserId == request.FromUserId &&
        x.ToUserId == request.ToUserId);

    if (oldSwipe == null)
    {
        db.Swipes.Add(new Swipe
        {
            FromUserId = request.FromUserId,
            ToUserId = request.ToUserId,
            IsLike = request.IsLike,
            CreatedAtUtc = DateTime.UtcNow
        });
    }
    else
    {
        oldSwipe.IsLike = request.IsLike;
        oldSwipe.CreatedAtUtc = DateTime.UtcNow;
    }

    await db.SaveChangesAsync();

    if (!request.IsLike)
        return Results.Ok(new SwipeResponse { Matched = false });

    var otherUserLikedMe = await db.Swipes.AnyAsync(x =>
        x.FromUserId == request.ToUserId &&
        x.ToUserId == request.FromUserId &&
        x.IsLike);

    if (!otherUserLikedMe)
        return Results.Ok(new SwipeResponse { Matched = false });

    var user1Id = Math.Min(request.FromUserId, request.ToUserId);
    var user2Id = Math.Max(request.FromUserId, request.ToUserId);

    var matchExists = await db.Matches.AnyAsync(x =>
        x.User1Id == user1Id &&
        x.User2Id == user2Id);

    if (!matchExists)
    {
        db.Matches.Add(new Match
        {
            User1Id = user1Id,
            User2Id = user2Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    return Results.Ok(new SwipeResponse { Matched = true });
});

app.MapGet("/matches/{userId:int}", async (int userId, AppDbContext db) =>
{
    if (userId <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı." });

    var matches = await db.Matches
        .AsNoTracking()
        .Where(x => x.User1Id == userId || x.User2Id == userId)
        .OrderByDescending(x => x.CreatedAtUtc)
        .ToListAsync();

    var otherUserIds = matches
        .Select(x => x.User1Id == userId ? x.User2Id : x.User1Id)
        .ToList();

    var users = await db.Users
        .AsNoTracking()
        .Where(x => otherUserIds.Contains(x.Id))
        .Select(x => new AppUserDto
        {
            Id = x.Id,
            DisplayName = x.Username,
            City = x.City,
            Age = x.Age,
            Gender = x.Gender,
            PhotoUrl = "",
            CreatedAt = x.CreatedAtUtc
        })
        .ToListAsync();

    return Results.Ok(users);
});

app.MapGet("/messages/{user1Id:int}/{user2Id:int}", async (int user1Id, int user2Id, AppDbContext db) =>
{
    if (user1Id <= 0 || user2Id <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı bilgileri." });

    var user1 = Math.Min(user1Id, user2Id);
    var user2 = Math.Max(user1Id, user2Id);

    var isMatched = await db.Matches.AnyAsync(x =>
        x.User1Id == user1 &&
        x.User2Id == user2);

    if (!isMatched)
        return Results.BadRequest(new { message = "Eşleşme yok. Mesajlar açılamaz." });

    var messages = await db.Messages
        .AsNoTracking()
        .Where(x =>
            (x.SenderUserId == user1Id && x.ReceiverUserId == user2Id) ||
            (x.SenderUserId == user2Id && x.ReceiverUserId == user1Id))
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

    return Results.Ok(messages);
});

app.MapPost("/messages", async (SendMessageRequest request, AppDbContext db) =>
{
    if (request.SenderUserId <= 0 ||
        request.ReceiverUserId <= 0 ||
        string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { message = "Geçersiz mesaj bilgileri." });
    }

    var user1 = Math.Min(request.SenderUserId, request.ReceiverUserId);
    var user2 = Math.Max(request.SenderUserId, request.ReceiverUserId);

    var isMatched = await db.Matches.AnyAsync(x =>
        x.User1Id == user1 &&
        x.User2Id == user2);

    if (!isMatched)
        return Results.BadRequest(new { message = "Eşleşme yok. Mesaj gönderilemez." });

    var message = new Message
    {
        SenderUserId = request.SenderUserId,
        ReceiverUserId = request.ReceiverUserId,
        Text = request.Text.Trim(),
        CreatedAtUtc = DateTime.UtcNow
    };

    db.Messages.Add(message);
    await db.SaveChangesAsync();

    return Results.Ok(new MessageDto
    {
        Id = message.Id,
        SenderUserId = message.SenderUserId,
        ReceiverUserId = message.ReceiverUserId,
        Text = message.Text,
        CreatedAt = message.CreatedAtUtc
    });
});

Console.WriteLine($"PORT: {port}");
app.Run();

public class CreateUserRequest
{
    public string DisplayName { get; set; } = "";
    public string City { get; set; } = "";
    public int Age { get; set; }
    public string Gender { get; set; } = "";
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

public class MessageDto
{
    public int Id { get; set; }
    public int SenderUserId { get; set; }
    public int ReceiverUserId { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
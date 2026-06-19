using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using LoyalAnimal.Server.Data;
using LoyalAnimal.Shared;

var builder = WebApplication.CreateBuilder(args);

// PORT
var port = Environment.GetEnvironmentVariable("PORT") ?? "5298";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// DATABASE
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL1") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL2");

databaseUrl = databaseUrl?.Trim();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        options.UseNpgsql(ConvertDatabaseUrl(databaseUrl));
    }
    else
    {
        options.UseSqlite("Data Source=loyalanimal.db");
    }
});

// CORS
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

var app = builder.Build();

// DB INIT
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("AllowAll");


// DEBUG DB
app.MapGet("/debug/db", () =>
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

    return Results.Ok(new
    {
        database = string.IsNullOrWhiteSpace(databaseUrl) ? "SQLite" : "PostgreSQL",
        hasDatabaseUrl = !string.IsNullOrWhiteSpace(databaseUrl),
        time = DateTime.UtcNow
    });
});

// ROOT

// Ensure PhotoUrl column exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        db.Database.ExecuteSqlRaw("""
            ALTER TABLE "Users"
            ADD COLUMN IF NOT EXISTS "PhotoUrl" text NOT NULL DEFAULT '';
        """);
    }
    catch
    {
        // SQLite/local fallback
        try
        {
            db.Database.ExecuteSqlRaw("""
                ALTER TABLE Users
                ADD COLUMN PhotoUrl TEXT NOT NULL DEFAULT '';
            """);
        }
        catch { }
    }
}

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        success = true,
        message = "LoyalAnimal API Running"
    });
});

// REGISTER
app.MapPost("/users/register", async (
    CreateUserRequest req,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.DisplayName) ||
        string.IsNullOrWhiteSpace(req.City) ||
        string.IsNullOrWhiteSpace(req.Gender) ||
        req.Age <= 0)
    {
        return Results.BadRequest(new { message = "Geçersiz kullanıcı" });
    }

    var username = req.DisplayName.Trim();
    var city = req.City.Trim();
    var gender = req.Gender.Trim();

    var existingUser = await db.Users.FirstOrDefaultAsync(x =>
        x.Username.ToLower() == username.ToLower() &&
        x.City.ToLower() == city.ToLower() &&
        x.Gender.ToLower() == gender.ToLower() &&
        x.Age == req.Age);

    if (existingUser != null)
        return Results.Ok(ToDto(existingUser));

    var user = new User
    {
        Username = username,
        City = city,
        Age = req.Age,
        Gender = gender,
        PhotoUrl = req.PhotoUrl?.Trim() ?? "",
        CreatedAtUtc = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(ToDto(user));
});

// USERS
app.MapGet("/users", async (AppDbContext db) =>
{
    var userEntities = await db.Users
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAtUtc)
        .ToListAsync();

    var users = userEntities.Select(ToDto).ToList();

    return Results.Ok(users);
});

// USER DETAIL
app.MapGet("/users/{id:int}", async (int id, AppDbContext db) =>
{
    var user = await db.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    if (user == null)
        return Results.NotFound(new { message = "Kullanıcı bulunamadı" });

    return Results.Ok(ToDto(user));
});


// UPDATE USER PHOTO
app.MapPost("/users/{id:int}/photo", async (
    int id,
    UpdatePhotoRequest req,
    AppDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);

    if (user == null)
        return Results.NotFound(new { message = "Kullanıcı bulunamadı" });

    user.PhotoUrl = string.IsNullOrWhiteSpace(req.PhotoUrl) ? user.PhotoUrl : req.PhotoUrl.Trim();

    await db.SaveChangesAsync();

    return Results.Ok(ToDto(user));
});

// DISCOVER
app.MapGet("/users/discover/{userId:int}", async (
    int userId,
    AppDbContext db) =>
{
    if (userId <= 0)
    {
        return Results.BadRequest(new
        {
            message = "Geçersiz kullanıcı"
        });
    }

    var swipedIds = await db.Swipes
        .AsNoTracking()
        .Where(x => x.FromUserId == userId)
        .Select(x => x.ToUserId)
        .ToListAsync();

    var userEntities = await db.Users
        .AsNoTracking()
        .Where(x =>
            x.Id != userId &&
            !swipedIds.Contains(x.Id))
        .OrderByDescending(x => x.CreatedAtUtc)
        .ToListAsync();

    var users = userEntities.Select(ToDto).ToList();

    if (users.Count == 0)
    {
        var fallbackEntities = await db.Users
            .AsNoTracking()
            .Where(x => x.Id != userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        users = fallbackEntities.Select(ToDto).ToList();
    }

    return Results.Ok(users);
});

// SWIPE
app.MapPost("/swipes", async (
    SwipeRequest req,
    AppDbContext db) =>
{
    if (req.FromUserId <= 0 || req.ToUserId <= 0)
        return Results.BadRequest(new { message = "Geçersiz kullanıcı" });

    if (req.FromUserId == req.ToUserId)
        return Results.BadRequest(new { message = "Kendini beğenemezsin" });

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

// MATCHES
app.MapGet("/matches/{userId:int}", async (
    int userId,
    AppDbContext db) =>
{
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
        
        .ToListAsync();

    return Results.Ok(users);
});

// WHO LIKED ME
app.MapGet("/likes/me/{userId:int}", async (
    int userId,
    AppDbContext db) =>
{
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
        
        .ToListAsync();

    return Results.Ok(users);
});

// GET MESSAGES
app.MapGet("/messages/{u1:int}/{u2:int}", async (
    int u1,
    int u2,
    AppDbContext db) =>
{
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

// SEND MESSAGE
app.MapPost("/messages", async (
    SendMessageRequest req,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { message = "Mesaj boş" });

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


// CLEAR DEFAULT PHOTOS
app.MapPost("/admin/clear-default-photos", async (AppDbContext db) =>
{
    var users = await db.Users.ToListAsync();

    foreach (var user in users)
    {
        if (!string.IsNullOrWhiteSpace(user.PhotoUrl) &&
            (user.PhotoUrl.Contains("picsum.photos") ||
             user.PhotoUrl.Contains("dicebear.com") ||
             user.PhotoUrl.Contains("placehold.co") ||
             user.PhotoUrl.StartsWith("/data/user/")))
        {
            user.PhotoUrl = "";
        }
    }

    await db.SaveChangesAsync();

    return Results.Ok("Default photos cleared");
});


// UPLOAD USER PHOTO TO CLOUDINARY
app.MapPost("/users/{id:int}/photo/upload", async (int id, HttpRequest request, AppDbContext db) =>
{
    var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
    var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
    var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");

    if (string.IsNullOrWhiteSpace(cloudName) ||
        string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(apiSecret))
    {
        return Results.BadRequest(new { message = "Cloudinary ayarları eksik" });
    }

    var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);

    if (user == null)
        return Results.NotFound(new { message = "Kullanıcı bulunamadı" });

    if (!request.HasFormContentType)
        return Results.BadRequest(new { message = "Form-data bekleniyor" });

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();

    if (file == null || file.Length == 0)
        return Results.BadRequest(new { message = "Dosya bulunamadı" });

    await using var stream = file.OpenReadStream();

    var account = new Account(cloudName, apiKey, apiSecret);
    var cloudinary = new Cloudinary(account);

    var uploadParams = new ImageUploadParams
    {
        File = new FileDescription(file.FileName, stream),
        Folder = "loyalanimal/users",
        PublicId = $"user_{id}_{DateTime.UtcNow.Ticks}",
        Overwrite = true
    };

    var uploadResult = await cloudinary.UploadAsync(uploadParams);

    if (uploadResult.Error != null)
        return Results.BadRequest(new { message = uploadResult.Error.Message });

    user.PhotoUrl = uploadResult.SecureUrl.ToString();
    await db.SaveChangesAsync();

    return Results.Ok(ToDto(user));
});

app.Run();

// PostgreSQL URL converter
static string ConvertDatabaseUrl(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);

    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var port = uri.Port > 0 ? uri.Port : 5432;

    return
        $"Host={uri.Host};" +
        $"Port={port};" +
        $"Database={uri.AbsolutePath.TrimStart('/')};" +
        $"Username={username};" +
        $"Password={password};" +
        $"SSL Mode=Require;" +
        $"Trust Server Certificate=true;";
}


// DTO
static AppUserDto ToDto(User u) => new()
{
    Id = u.Id,
    DisplayName = u.Username,
    City = u.City,
    Age = u.Age,
    Gender = u.Gender,
    PhotoUrl = u.PhotoUrl ?? "",
    CreatedAt = u.CreatedAtUtc
};

public class CreateUserRequest
{
    public string DisplayName { get; set; } = "";
    public string City { get; set; } = "";
    public int Age { get; set; }
    public string Gender { get; set; } = "";
    public string PhotoUrl { get; set; } = "";
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


public class UpdatePhotoRequest
{
    public string PhotoUrl { get; set; } = "";
}





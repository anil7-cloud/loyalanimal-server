using Microsoft.EntityFrameworkCore;
using LoyalAnimal.Server.Data;
using LoyalAnimal.Server.Services;
using LoyalAnimal.Shared;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5298";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// SQLite (Render uyumlu)
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


// ✅ TEK VE DOĞRU DB INIT
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("AllowAll");


// ✅ ROOT
app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        success = true,
        message = "LoyalAnimal API Running"
    });
});


// ✅ USER REGISTER
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

    return Results.Ok(ToDto(user));
});


// ✅ USERS LIST
app.MapGet("/users", async (AppDbContext db) =>
{
    var users = await db.Users
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => ToDto(x))
        .ToListAsync();

    return Results.Ok(users);
});


// ✅ DISCOVER
app.MapGet("/users/discover/{userId:int}", async (int userId, AppDbContext db) =>
{
    if (userId <= 0)
        return Results.BadRequest();

    var users = await db.Users
        .AsNoTracking()
        .Where(x => x.Id != userId)
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => ToDto(x))
        .ToListAsync();

    return Results.Ok(users);
});


// ✅ SWIPE (basit versiyon)
app.MapPost("/swipes", async (SwipeRequest request, AppDbContext db) =>
{
    if (request.FromUserId <= 0 || request.ToUserId <= 0)
        return Results.BadRequest();

    try
    {
        db.Swipes.Add(new Swipe
        {
            FromUserId = request.FromUserId,
            ToUserId = request.ToUserId,
            IsLike = request.IsLike,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
    catch { }

    return Results.Ok(new SwipeResponse { Matched = request.IsLike });
});


// ✅ RUN
Console.WriteLine($"PORT: {port}");
app.Run();


// ================= DTO =================

static AppUserDto ToDto(User user)
{
    return new AppUserDto
    {
        Id = user.Id,
        DisplayName = user.Username,
        City = user.City,
        Age = user.Age,
        Gender = user.Gender,
        PhotoUrl = "",
        CreatedAt = user.CreatedAtUtc
    };
}

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
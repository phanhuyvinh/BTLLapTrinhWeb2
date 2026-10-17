using MedCare_Pharmacy.Models.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. DbContext SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. CORS - Cho phép kết nối từ Frontend Live Server (http://localhost:5500)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5500", "http://127.0.0.1:5500", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    jwtKey = LoadOrCreateJwtKey();
    Console.WriteLine("JWT signing key loaded from the local application data directory.");
}
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key phải có ít nhất 32 byte. Hãy cung cấp khóa an toàn qua User Secrets hoặc biến môi trường Jwt__Key.");
}
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
            if (!int.TryParse(id, out var userId) || string.IsNullOrWhiteSpace(role))
            {
                context.Fail("Invalid account claims.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var account = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Role, u.Status })
                .SingleOrDefaultAsync();
            if (account == null || !string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(account.Role, role, StringComparison.OrdinalIgnoreCase))
                context.Fail("Account is inactive or its permissions have changed.");
        }
    };
});

// Server-rendered staff pages use an HttpOnly cookie; APIs continue to use JWT bearer tokens.
builder.Services.AddAuthentication().AddCookie("PageCookie", options =>
{
    options.Cookie.Name = "medcare_page_auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Customer/Login";
    options.AccessDeniedPath = "/Home/Index";
    options.SlidingExpiration = false;
    options.Events.OnValidatePrincipal = async context =>
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
        if (!int.TryParse(id, out var userId) || string.IsNullOrWhiteSpace(role))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync("PageCookie");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var account = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Role, u.Status }).SingleOrDefaultAsync();
        if (account == null || !string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(account.Role, role, StringComparison.OrdinalIgnoreCase))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync("PageCookie");
        }
    };
});

// 4. Đăng ký Services cho API & MVC
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("order-create", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();

// 5. Cấu hình Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
// Older prescription uploads were under wwwroot. Require the protected API route even for these files.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads/prescriptions", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
app.UseStaticFiles(); // Phục vụ file CSS/JS/Images từ wwwroot

app.UseRouting();
app.UseRateLimiter();

// Kích hoạt CORS
app.UseCors("AllowFrontend");

// Kích hoạt Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 6. Cấu hình Routing chuẩn
// Map Route cho MVC Views (Khắc phục lỗi 'MapControllers' chứa 'name')
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map Route cho Web API Controllers ([ApiController])
app.MapControllers();

app.Run();

static string LoadOrCreateJwtKey()
{
    var dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MedCarePharmacy");
    Directory.CreateDirectory(dataDirectory);

    if (!OperatingSystem.IsWindows())
        File.SetUnixFileMode(dataDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

    var keyPath = Path.Combine(dataDirectory, "jwt-signing-key.txt");
    if (File.Exists(keyPath))
        return ReadAndValidateKey(keyPath);

    var generatedKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    try
    {
        using var stream = new FileStream(keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(generatedKey);
    }
    catch (IOException) when (File.Exists(keyPath))
    {
        // Another app instance may have created the key at the same time.
        return ReadAndValidateKey(keyPath);
    }

    if (!OperatingSystem.IsWindows())
        File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

    return generatedKey;
}

static string ReadAndValidateKey(string keyPath)
{
    var key = File.ReadAllText(keyPath, Encoding.UTF8).Trim();
    try
    {
        if (Convert.FromBase64String(key).Length >= 32)
            return key;
    }
    catch (FormatException)
    {
        // Report a clear setup error below rather than silently rotating the key.
    }

    throw new InvalidOperationException($"JWT key file '{keyPath}' is invalid. Remove or repair it before restarting; replacing it invalidates existing tokens.");
}

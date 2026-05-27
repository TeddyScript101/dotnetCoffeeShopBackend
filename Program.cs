using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Events.Consumers;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Read allowed origins from env var in production (comma-separated)
// e.g. CORS_ORIGINS=https://your-frontend.vercel.app,http://localhost:5173
var allowedOrigins = builder.Configuration["CorsOrigins"]
    ?? "http://localhost:5173";
var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token (without 'Bearer ' prefix)"
    });
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});

// Configure EF Core — SQLite in-memory for Testing, PostgreSQL (Neon) everywhere else.
// In Testing mode the DbContext is registered by CustomWebApplicationFactory instead,
// which passes a unique per-test-class database name via configuration.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<CoffeeShopDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            // Give Neon free-tier compute up to 60 s to wake up before timing out
            npgsql => npgsql.CommandTimeout(60)));
}

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<CoffeeShopDbContext>()
    .AddDefaultTokenProviders();

// Configure JWT Authentication
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT signing key (Jwt:Key) is not configured.")))
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var jti = context.Principal?.FindFirstValue("jti");
            if (jti is null) return Task.CompletedTask;
            var blacklist = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklist>();
            if (blacklist.IsRevoked(jti))
                context.Fail("Token has been revoked.");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSingleton<ITokenBlacklist, InMemoryTokenBlacklist>();
builder.Services.AddRateLimiter(options =>
{
    // Max 10 login attempts per IP per minute
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Register MassTransit — RabbitMQ in local dev, in-memory on Render (no broker configured)
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<OrderStatusChangedConsumer>();

    var rabbitMqHost = builder.Configuration["RabbitMQ:Host"];

    if (!string.IsNullOrEmpty(rabbitMqHost))
    {
        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(rabbitMqHost, "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            // Auto-creates queues and exchanges based on consumer class names
            cfg.ConfigureEndpoints(ctx);
        });
    }
    else
    {
        // No broker configured — process messages in-process (suitable for single-instance deployments)
        x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
    }
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

var app = builder.Build();

// Run migrations and seed data on startup.
// Neon free tier also has a cold start (up to ~30 s). Retry with backoff so
// a slow DB wake-up doesn't crash the app and trigger Render's error page.
using (var scope = app.Services.CreateScope())
{
    const int maxAttempts = 6;
    const int retryDelayMs = 8_000; // 8 s per retry → up to ~40 s total wait

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

            // Use EnsureCreated in Testing (SQLite in-memory), MigrateAsync everywhere else
            if (app.Environment.IsEnvironment("Testing"))
                await db.Database.EnsureCreatedAsync();
            else
                await db.Database.MigrateAsync();

            await RoleSeeder.SeedRolesAsync(scope.ServiceProvider);

            // Skip auto-seeding demo data in tests — test classes seed their own data
            if (!app.Environment.IsEnvironment("Testing"))
                await DatabaseSeeder.SeedDataAsync(scope.ServiceProvider);

            break; // success — exit retry loop
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex,
                "DB init attempt {Attempt}/{Max} failed (Neon cold start?). Retrying in {Delay} ms...",
                attempt, maxAttempts, retryDelayMs);
            await Task.Delay(retryDelayMs);
        }
        // Last attempt: let the exception propagate so the crash is visible in Render logs
    }
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
    });
});

// MapOpenApi serves the raw spec at /openapi/v1.json
// Scalar serves the interactive UI at /scalar/v1 (works in all environments)
// Swagger UI is only used in Development (Swashbuckle 10.x has static file issues in production)
app.MapOpenApi();

// Swagger UI at /swagger
app.UseSwagger();
app.UseSwaggerUI();

// Scalar UI at /scalar/v1
app.MapScalarApiReference(options =>
{
    options.WithOpenApiRoutePattern("/openapi/v1.json");
    options.WithTitle("Coffee Shop API");
});

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Minimal keep-warm endpoint for cron-job pinging (e.g. cron-job.org)
// Registered before other middleware so it responds immediately, even during cold start
app.MapGet("/ping", () => "pong")
   .AllowAnonymous()
   .ExcludeFromDescription();

// Health check endpoint for frontend warm-up detection
// RequireCors ensures CORS headers are sent even in Brave with Shields up
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous()
   .RequireCors("AllowFrontend")
   .WithTags("Health");

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }

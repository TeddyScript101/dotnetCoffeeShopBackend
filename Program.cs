using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Events.Consumers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using MassTransit;

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

// Configure EF Core with PostgreSQL (Neon)
builder.Services.AddDbContext<CoffeeShopDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "ThisIsAVerySecretKeyForJwtAuthenticationWhichShouldBeLongEnough"))
    };
});

builder.Services.AddAuthorization();

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

// Run migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
    await db.Database.MigrateAsync();
    await RoleSeeder.SeedRolesAsync(scope.ServiceProvider);
    await DatabaseSeeder.SeedDataAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
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

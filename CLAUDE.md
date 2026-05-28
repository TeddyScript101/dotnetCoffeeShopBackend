# CoffeeShopApi

ASP.NET Core 10 REST API for a coffee shop e-commerce platform.

## Tech Stack

- **Runtime**: .NET 10
- **Framework**: ASP.NET Core Web API
- **Database**: PostgreSQL via Neon (EF Core code-first migrations)
- **Auth**: ASP.NET Identity + JWT Bearer
- **Message bus**: MassTransit + RabbitMQ (in-memory fallback on Render free tier)
- **API docs**: Swagger UI (`/swagger`) + Scalar (`/scalar/v1`) — public in all environments (portfolio)
- **Tests**: xUnit + WebApplicationFactory + SQLite in-memory (test project lives at `../CoffeeShopApi.Tests/`)

## Quick Start

```bash
# Requires: .NET 10 SDK

# Set required env vars
export Jwt__Key="your-secret-key-minimum-32-chars"
export ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"

# Optional: start RabbitMQ for event bus (falls back to in-memory without it)
docker compose up -d

dotnet run
# API:     http://localhost:5000
# Swagger: http://localhost:5000/swagger
# Scalar:  http://localhost:5000/scalar/v1
```

## Running Tests

Tests use SQLite in-memory — no external DB needed. The test project is a sibling directory.

```bash
cd ../CoffeeShopApi.Tests
dotnet test
```

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `Jwt__Key` | Yes | JWT signing key (min 32 chars). App fails to start without it. |
| `ConnectionStrings__DefaultConnection` | Yes | Neon PostgreSQL connection string |
| `CorsOrigins` | No | Comma-separated allowed origins (default: `http://localhost:5173`) |
| `RabbitMQ__Host` | No | RabbitMQ host — omit to use in-memory transport |

## Project Structure

```
Controllers/   — API endpoints
  AuthController       — register, login, logout, assign-role
  AccountController    — profile, change-password
  OrdersController     — place order, list/get customer orders
  AdminOrdersController — list all orders, update order status
  ProductsController   — list products (public)
Data/          — CoffeeShopDbContext, seeders
DTOs/          — request/response shapes (never bind directly to Models)
Events/        — MassTransit integration events + consumers
Migrations/    — EF Core migrations (auto-applied on startup)
Models/        — Domain entities
Services/      — ITokenBlacklist (in-memory JWT revocation for logout)
```

## Key Conventions

- All controllers: `[ApiController]` + `[Route("api/[controller]")]`
- Constructor-inject: `CoffeeShopDbContext`, `UserManager<ApplicationUser>` (if needed), `ILogger<T>`
- Customer data is ALWAYS filtered by `userId` from JWT claims (IDOR prevention)
- Admin routes: `[Authorize(Roles = "Admin")]`
- Valid roles: `"Admin"`, `"Customer"` (enforced in AssignRole whitelist)
- DTOs for all input/output — never expose Model classes directly
- Migrations applied automatically via `MigrateAsync()` on startup (with Neon cold-start retry)

## Deployment

Deployed on **Render** via Docker (`render.yaml`).
Database: Neon free tier PostgreSQL (up to 60s cold start — handled with exponential retry on startup).
Health check: `GET /health`
Keep-warm ping: `GET /ping`

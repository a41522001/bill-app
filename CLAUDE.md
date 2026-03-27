# Bill-App - Project Instructions

## Project Overview

Personal finance management API (記帳應用程式), built with ASP.NET Core 10 / C# 14.
Monorepo solution with two projects: `Bill-App-API` (Web API) and `Bill-App-Cache` (Redis class library).

## Tech Stack

- **Runtime**: .NET 10, C# 14
- **Database**: PostgreSQL 18.1 (via EF Core 10 + Npgsql)
- **Cache**: Redis 7.4 (docker container, service not yet implemented)
- **Auth**: JWT Bearer + BCrypt password hashing
- **Infra**: Docker Compose (postgres + redis)

## Build & Run

```bash
# Start database and cache containers
docker compose up -d

# Run API (from project root)
dotnet run --project Bill-App-API

# Run with hot reload
dotnet watch run --project Bill-App-API

# Add EF Core migration
dotnet ef migrations add <MigrationName> --project Bill-App-API

# Apply migrations
dotnet ef database update --project Bill-App-API
```

- API runs on `http://localhost:5148` / `https://localhost:7188`
- Swagger UI available at `/swagger` in Development mode

## Project Structure

```
Bill-App-API/
├── Controllers/    # HTTP endpoints
├── Services/       # Business logic
│   └── Interfaces/ # Service contracts
├── Models/         # EF Core entities
├── Dtos/           # Request/Response records
├── Enums/          # TransactionTypeEnum (Income=0, Expense=1)
├── Contexts/       # BillDbContext
├── Utils/          # PasswordHasher (BCrypt wrapper)
├── Migrations/     # EF Core migrations
└── Program.cs      # DI registration & middleware pipeline

Bill-App-Cache/
├── IRedisService.cs
└── RedisService.cs
```

## Architecture & Conventions

- **Layered architecture**: Controller -> Service -> DbContext (no Repository layer)
- **DI lifetime**: All services registered as **Scoped**
- **Namespace root**: `Bill_App` (underscore, not hyphen)
- **Interface prefix**: `I` (IUserService, ICategoryService, IJwtService)
- **DTOs**: Use C# `record` types for request objects
- **Models**: Return domain entities directly (no output DTOs / no AutoMapper)
- **Async pattern**: All I/O operations must be async (`Task<T>`)
- **EF Core**: Code-first approach with explicit migrations
- **Nullable reference types**: Enabled project-wide

## Environment Variables

Required in `.env` (loaded via DotNetEnv):

```
DB_USER=<postgres user>
DB_PASSWORD=<postgres password>
DB_NAME=<database name>
```

JWT config in `appsettings.json` or environment:

```
JWT__KEY, JWT__ISSUER, JWT__AUDIENCE, JWT__DURATION_IN_MINUTES
```

## Important Notes

- **Ignore `bin/` and `obj/` folders** when scanning or searching the codebase
- `.env` files are gitignored - never commit secrets
- `Bill-App-Cache` project is scaffolded but not yet implemented
- `.github/workflows/` exists but has no CI/CD pipelines yet

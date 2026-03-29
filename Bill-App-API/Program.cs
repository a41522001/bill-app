using Bill_App.Contexts;
using Bill_App.Interfaces;
using Bill_App.Options;
using Bill_App.Services;
using Bill_App.Services.Interfaces;
using Bill_App_API.Middlewares;
using Bill_App_Cache.Interface;
using Bill_App_Cache.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
Env.Load("../.env");
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
// Database
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var connectionString = $"Host=localhost;Port=5432;Database={dbName};Username={dbUser};Password={dbPassword}";

builder.Services.AddDbContext<BillDbContext>(options =>
    options.UseNpgsql(connectionString));
// Options
builder.Services.Configure<JwtOptions>(options =>
{
    options.Key = Environment.GetEnvironmentVariable("JWT__KEY")!;
    options.Issuer = Environment.GetEnvironmentVariable("JWT__ISSUER")!;
    options.Audience = Environment.GetEnvironmentVariable("JWT__AUDIENCE")!;
    options.DurationInMinutes = int.Parse(
        Environment.GetEnvironmentVariable("JWT__DURATION_IN_MINUTES") ?? "15");
});

// Dependence Injection
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));
builder.Services.AddScoped<IRedisService, RedisService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<TokenMiddleware>();
//app.UseAuthentication();
//app.UseAuthorization();
app.MapControllers();
app.Run();

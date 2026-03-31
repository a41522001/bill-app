using Bill_App_API.Contexts;
using Bill_App_API.Interfaces;
using Bill_App_API.Options;
using Bill_App_API.Services;
using Bill_App_API.Middlewares;
using Bill_App_Cache.Interface;
using Bill_App_Cache.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
Env.Load("../.env");
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
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
//
builder.Services.Configure<MaxDeviceOptions>(options =>
{
  options.MaxDevice = int.Parse(
      Environment.GetEnvironmentVariable("MAX_DEVICE") ?? "5");
});
// CORS

// Dependence Injection
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));
builder.Services.AddScoped<IRedisService, RedisService>();

// Filter
builder.Services.AddControllers(options =>
{
  options.Filters.Add<Bill_App_API.Filters.LogActionFilter>();
  options.Filters.Add<Bill_App_API.Filters.GlobalExceptionFilter>();
  options.Filters.Add<Bill_App_API.Filters.ResultWrapFilter>();
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<AccessTokenMiddleware>();
app.UseMiddleware<RefreshTokenMiddleware>();
//app.UseAuthentication();
//app.UseAuthorization();
app.MapControllers();
app.Run();

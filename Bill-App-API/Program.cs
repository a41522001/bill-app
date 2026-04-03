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
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
// Database
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var connectionString = $"Host=localhost;Port=5432;Database={dbName};Username={dbUser};Password={dbPassword}";
builder.Services.AddDbContext<BillDbContext>(options =>
    options.UseNpgsql(connectionString));
// #region Options
// JWT Options
builder.Services.Configure<JwtOptions>(options =>
{
    options.Key = Environment.GetEnvironmentVariable("JWT__KEY")!;
    options.Issuer = Environment.GetEnvironmentVariable("JWT__ISSUER")!;
    options.Audience = Environment.GetEnvironmentVariable("JWT__AUDIENCE")!;
    options.DurationInMinutes = int.Parse(
        Environment.GetEnvironmentVariable("JWT__DURATION_IN_MINUTES") ?? "15");
});
// Device Options
builder.Services.Configure<MaxDeviceOptions>(options =>
{
    options.MaxDevice = int.Parse(
        Environment.GetEnvironmentVariable("MAX_DEVICE") ?? "5");
});
// Refresh Token Options
builder.Services.Configure<RefreshTokenOptions>(options =>
{
    options.DurationInDay = int.Parse(
        Environment.GetEnvironmentVariable("REFRESH_TOKEN__DURATION_IN_DAY") ?? "7");
    options.OldTokenGraceInSeconds = int.Parse(
        Environment.GetEnvironmentVariable("REFRESH_TOKEN__OLD_TOKEN_GRACE_IN_SECONDS") ?? "15");
});
// User Cache Options
builder.Services.Configure<UserCacheOptions>(options =>
{
    options.TtlInHours = int.Parse(
        Environment.GetEnvironmentVariable("USER_CACHE__TTL_IN_HOURS") ?? "24");
});
// User Verify Email Options
builder.Services.Configure<UserVerifyEmailOptions>(options =>
{
    options.TtlInHours = int.Parse(
        Environment.GetEnvironmentVariable("USER_VERIFY_CACHE__TTL_IN_HOURS") ?? "1");
});
// App Options
builder.Services.Configure<AppOptions>(options =>
{
    options.Domain = Environment.GetEnvironmentVariable("APP_DOMAIN") ?? "";
});
// Frontend Options
builder.Services.Configure<FrontendOptions>(options =>
{
    options.Url = Environment.GetEnvironmentVariable("FRONT_END_URL") ?? "";
});
// Google Auth Options
builder.Services.Configure<GoogleAuthOptions>(options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("GOOGLE_AUTH_CLIENT_ID") ?? "";
});
// SMTP Options
builder.Services.Configure<SmtpOptions>(options =>
{
    options.Host = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "";
    options.Port = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
    options.SenderEmail = Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL") ?? "";
    options.SenderName = Environment.GetEnvironmentVariable("SMTP_SENDER_NAME") ?? "";
    options.Password = Environment.GetEnvironmentVariable("SMTP_SENDER_PASSWORD") ?? "";
});
// Cookie Options
builder.Services.Configure<AuthCookieOptions>(options =>
{
    options.SameSite = builder.Environment.IsProduction() ? SameSiteMode.Strict : SameSiteMode.None;
});
// #endregion

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(Environment.GetEnvironmentVariable("FRONT_END_URL"))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Dependence Injection
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));
builder.Services.AddScoped<IRedisService, RedisService>();

// Filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<Bill_App_API.Filters.LogActionFilter>();
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

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors();
app.UseMiddleware<AccessTokenMiddleware>();
app.UseMiddleware<RefreshTokenMiddleware>();
app.MapControllers();
app.Run();

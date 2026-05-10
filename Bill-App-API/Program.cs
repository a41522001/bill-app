using Amazon.S3;
using Amazon.S3.Model;
using Bill_App_API.Contexts;
using Bill_App_API.Interfaces;
using Bill_App_API.Middlewares;
using Bill_App_API.Options;
using Bill_App_API.Services;
using Bill_App_Cache.Interface;
using Bill_App_Cache.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "../.env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
// Database
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";
builder.Services.AddDbContext<BillDbContext>(options =>
    options.UseNpgsql(connectionString));
// #region Options
// JWT Options
var jwtSetting = builder.Configuration.GetSection("JWT");
builder.Services.Configure<JwtOptions>(options =>
{
    options.Key = Environment.GetEnvironmentVariable("JWT_KEY")!;
    options.Issuer = jwtSetting.GetValue<string>("Issuer")!;
    options.Audience = jwtSetting.GetValue<string>("Audience")!;
    options.DurationInMinutes = jwtSetting.GetValue<int>("DurationInMinutes");
});
// Refresh Token Options
var refreshTokenSetting = builder.Configuration.GetSection("RefreshToken");
builder.Services.Configure<RefreshTokenOptions>(options =>
{
    options.DurationInDays = refreshTokenSetting.GetValue<int>("DurationInDays");
    options.OldTokenGraceInSeconds = refreshTokenSetting.GetValue<int>("OldTokenGraceInSeconds");
});
// Device Options
var deviceSetting = builder.Configuration.GetSection("Device");
builder.Services.Configure<MaxDeviceOptions>(options =>
{
    options.MaxDevice = deviceSetting.GetValue<int>("MaxDevice");
});
// User Cache Options
var userCacheSetting = builder.Configuration.GetSection("UserCache");
builder.Services.Configure<UserCacheOptions>(options =>
{
    options.TtlInHours = userCacheSetting.GetValue<int>("TtlInHours");
});
// User Verify Email Options
var userVerifyEmailSetting = builder.Configuration.GetSection("UserVerifyEmailCache");
builder.Services.Configure<UserVerifyEmailOptions>(options =>
{
    options.TtlInHours = userVerifyEmailSetting.GetValue<int>("TtlInHours");
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
var smtpSetting = builder.Configuration.GetSection("SMTP");
builder.Services.Configure<SmtpOptions>(options =>
{
    options.Host = smtpSetting.GetValue<string>("Host")!;
    options.Port = smtpSetting.GetValue<int>("Port");
    options.SenderName = smtpSetting.GetValue<string>("SenderName")!;
    options.SenderEmail = Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL") ?? "";
    options.Password = Environment.GetEnvironmentVariable("SMTP_SENDER_PASSWORD") ?? "";
});
// Cookie Options
builder.Services.Configure<AuthCookieOptions>(options =>
{
    options.SameSite = builder.Environment.IsDevelopment()
        ? SameSiteMode.None
        : SameSiteMode.Strict;
});
// Login Rate Limit
var loginRateLimit = builder.Configuration.GetSection("LoginRateLimit");
builder.Services.Configure<LoginRateLimitOptions>(options =>
{
    options.IpLimit = loginRateLimit.GetValue<int>("IP");
    options.EmailLimit = loginRateLimit.GetValue<int>("Email");
    options.TtlMinute = loginRateLimit.GetValue<int>("TtlMinutes");
});
// #endregion

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(Environment.GetEnvironmentVariable("FRONT_END_URL")!)
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
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

// Avatar儲存策略 develop = local,  production = S3
var storageProvider = Environment.GetEnvironmentVariable("STORAGE_PROVIDER") ?? "Local";
if (storageProvider == "S3")
{
    // AWS
    builder.Services.Configure<AwsOptions>(options =>
    {
        options.AccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
        options.SecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
        options.Region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "";
    });
    // S3
    builder.Services.Configure<S3Options>(options =>
    {
        options.BucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME") ?? "";
        options.AvatarFolder = Environment.GetEnvironmentVariable("S3_BUCKET_AVATAR_FOLDER") ?? "";
        options.CloudFrontUrl = Environment.GetEnvironmentVariable("CLOUD_FRONT_URL") ?? "";
    });
    builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
    builder.Services.AddSingleton<IAmazonS3>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
        return new AmazonS3Client(
            opts.AccessKeyId,
            opts.SecretAccessKey,
            Amazon.RegionEndpoint.GetBySystemName(opts.Region)
        );
    });
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}

// Redis
var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddScoped<IRedisService, RedisService>();

// Filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<Bill_App_API.Filters.LogActionFilter>();
    options.Filters.Add<Bill_App_API.Filters.ResultWrapFilter>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Configure the HTTP request pipeline.
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}
else
{
    // 自動執行 EF Core migration（Production 環境）
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BillDbContext>();
    db.Database.Migrate();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStaticFiles();
app.UseCors();
app.UseMiddleware<LoginRateLimitMiddleware>();
app.UseMiddleware<AccessTokenMiddleware>();
app.UseMiddleware<RefreshTokenMiddleware>();
app.MapGet("/health", () => Results.Ok("healthy"));
app.MapControllers();
app.Run();

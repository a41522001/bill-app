using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Enums;
using Bill_App_API.Exceptions;
using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Bill_App_API.Options;
using Bill_App_API.Services;
using Bill_App_API.Utils;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
namespace Bill_App_Tests.Services;

public class UserServiceTest
{
    private readonly BillDbContext _dbContext;
    private readonly UserService _userService;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private static BillDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new BillDbContext(options);
    }
    public UserServiceTest()
    {
        _dbContext = CreateDbContext();
        _emailServiceMock = new Mock<IEmailService>();
        _redisServiceMock = new Mock<IRedisService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();

        _userService = new UserService(
            _dbContext,
            _redisServiceMock.Object,
            _tokenServiceMock.Object,
            _emailServiceMock.Object,
            _fileStorageServiceMock.Object,
            Options.Create(new MaxDeviceOptions { MaxDevice = 5 }),
            Options.Create(new RefreshTokenOptions { DurationInDay = 7 }),
            Options.Create(new UserCacheOptions { TtlInHours = 24 }),
            Options.Create(new UserVerifyEmailOptions { TtlInHours = 1 }),
            Options.Create(new GoogleAuthOptions { ClientId = "test" }),
            Options.Create(new FrontendOptions { Url = "http://localhost:5173" })
        );
    }
    ///<summary>
    ////// 測試 ResendVerifyEmail 功能的各種情況，包括：
    ///</summary>
    #region 
    public static IEnumerable<object[]> UserData =>
    [
        [new User { Id = Guid.NewGuid(), Name = "Test1", Email = "test1@test1.com", AuthProvider = AuthProviderEnum.Google, IsEmailVerified = true}],
        [new User { Id = Guid.NewGuid(), Name = "Test2", Email = "test2@test2.com", AuthProvider = AuthProviderEnum.Local, IsEmailVerified = true}]
    ];
    [Theory]
    [MemberData(nameof(UserData))]
    public async Task ResendVerifyEmail_UserStatusNotResend(User user)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _userService.ResendVerifyEmail(user.Email);
        _redisServiceMock.Verify(r => r.GetEmailResendCooldown(user.Id), Times.Never);
    }
    [Fact]
    public async Task ResendVerifyEmail_UserNotFound()
    {
        await _userService.ResendVerifyEmail("test@test.com");
        _redisServiceMock.Verify(r => r.GetEmailResendCooldown(It.IsAny<Guid>()), Times.Never);
    }
    [Fact]
    public async Task ResendVerifyEmail_CooldownIsExist()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = false
        };
        _redisServiceMock.Setup(r => r.GetEmailResendCooldown(userId)).ReturnsAsync(true);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _userService.ResendVerifyEmail(user.Email);
        _redisServiceMock.Verify(r => r.GetEmailResendCooldown(userId), Times.Once);
        _redisServiceMock.Verify(r => r.SetEmailResendCooldown(It.IsAny<Guid>(), It.IsAny<TimeSpan>()), Times.Never);
        _redisServiceMock.Verify(r => r.SetEmailVerifyTokenAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>()), Times.Never);
    }
    [Fact]
    public async Task ResendVerifyEmail_Success()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = false
        };
        _redisServiceMock.Setup(r => r.GetEmailResendCooldown(userId)).ReturnsAsync(false);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _userService.ResendVerifyEmail(user.Email);
        _redisServiceMock.Verify(r => r.GetEmailResendCooldown(userId), Times.Once);
        _redisServiceMock.Verify(r => r.SetEmailResendCooldown(user.Id, It.IsAny<TimeSpan>()), Times.Once);
        _redisServiceMock.Verify(r => r.SetEmailVerifyTokenAsync(It.IsAny<Guid>(), user.Id, It.IsAny<TimeSpan>()), Times.Once);
        _emailServiceMock.Verify(e => e.SendAsync(user.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
    #endregion
    // Signup
    [Fact]
    public async Task Signup_EmailAlreadyExist()
    {
        var user = new UserSignupRequest(
            Name: "test",
            Email: "aaa@aaa.com",
            Password: "password"
        );
        _dbContext.Users.Add(new User
        {
            Name = user.Name,
            Email = user.Email,
            Password = PasswordHasher.HashPassword(user.Password),
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = false,
        });
        await _dbContext.SaveChangesAsync();
        var actual = await _userService.Signup(user);
        Assert.False(actual);
        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        var count = await _dbContext.Users.CountAsync();
        Assert.Equal(1, count);
    }
    [Fact]
    public async Task Signup_Success()
    {
        var password = "password";
        var email = "aaa@aaa.com";
        var user = new UserSignupRequest(
            Name: "test",
            Email: email,
            Password: password
        );
        var actual = await _userService.Signup(user);
        Assert.True(actual);
        var count = await _dbContext.Users.CountAsync();
        Assert.Equal(1, count);
        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _redisServiceMock.Verify(r => r.SetEmailVerifyTokenAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>()), Times.Once);
        var result = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(result);
        Assert.NotEqual(password, result.Password);
    }
    // Logout
    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    public async Task Logout_RefreshTokenTransformError(string input)
    {
        await _userService.Logout(input);
        _redisServiceMock.Verify(r => r.GetRefreshToken(It.IsAny<Guid>()), Times.Never);
    }
    [Fact]
    public async Task Logout_UserHashIsNull()
    {
        _redisServiceMock.Setup(r => r.GetRefreshToken(It.IsAny<Guid>())).ReturnsAsync((RefreshTokenHash?)null);
        var refreshToken = Guid.NewGuid().ToString();
        await _userService.Logout(refreshToken);
        _redisServiceMock.Verify(r => r.DeleteUserRefreshTokenByMember(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        _redisServiceMock.Verify(r => r.DeleteRefreshToken(It.IsAny<Guid>()), Times.Never);
    }
    [Fact]
    public async Task Logout_Success()
    {
        var userId = Guid.NewGuid();
        var refreshToken = Guid.NewGuid().ToString();
        var userHash = new RefreshTokenHash(
            UserId: userId,
            Expire: DateTime.UtcNow.AddDays(7).ToString("o"),
            Email: "test@test.com",
            Sub: Guid.NewGuid(),
            Name: "test",
            IsOld: IsOldType.No
        );
        _redisServiceMock.Setup(r => r.GetRefreshToken(It.IsAny<Guid>())).ReturnsAsync(userHash);
        await _userService.Logout(refreshToken);
        _redisServiceMock.Verify(r => r.DeleteUserRefreshTokenByMember(userHash.UserId, It.IsAny<Guid>()), Times.Once);
        _redisServiceMock.Verify(r => r.DeleteRefreshToken(It.IsAny<Guid>()), Times.Once);
    }
    // Login
    [Fact]
    public async Task Login_UserIsNull()
    {
        var req = new UserLoginRequest(
            Email: "test@test.com",
            Password: "password"
        );
        var excepiton = await Assert.ThrowsAsync<ApiException>(async () => await _userService.Login(req));
        Assert.Equal("帳號或密碼錯誤", excepiton.Message);
    }
    [Fact]
    public async Task Login_AccountBoundGoogle()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "test",
            Email = "test@test.com",
            AuthProvider = AuthProviderEnum.Google,
            IsEmailVerified = true
        };
        var req = new UserLoginRequest(
            Email: user.Email,
            Password: user.Password
        );
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var excepiton = await Assert.ThrowsAsync<ApiException>(async () => await _userService.Login(req));
        Assert.Equal("該帳號已綁定 Google，請用 Google 登入", excepiton.Message);
        Assert.Equal(ResponseCodeEnum.AccountBoundToGoogle, excepiton.Code);
    }
    [Fact]
    public async Task Login_PasswordVerifyError()
    {
        var userId = Guid.NewGuid();
        var correctPassword = PasswordHasher.HashPassword("password");
        var errorPassword = "errorPassword";
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = true,
            Password = correctPassword
        };
        var req = new UserLoginRequest(
            Email: user.Email,
            Password: errorPassword
        );
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var actual = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var excepiton = await Assert.ThrowsAsync<ApiException>(async () => await _userService.Login(req));
        Assert.Equal("帳號或密碼錯誤", excepiton.Message);
        Assert.NotNull(actual);
        Assert.False(PasswordHasher.VerifyPassword(errorPassword, actual.Password));
    }
    [Fact]
    public async Task Login_EmailNotVerify()
    {
        var userId = Guid.NewGuid();
        var password = "password";
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = false,
            Password = PasswordHasher.HashPassword(password)
        };
        var req = new UserLoginRequest(
            Email: user.Email,
            Password: password
        );
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var actual = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var excepiton = await Assert.ThrowsAsync<ApiException>(async () => await _userService.Login(req));
        Assert.Equal("信箱未驗證", excepiton.Message);
        Assert.Equal(ResponseCodeEnum.EmailNotVerified, excepiton.Code);
        Assert.NotNull(actual);
        Assert.False(actual.IsEmailVerified);
    }
    [Fact]
    public async Task Login_Success()
    {
        var userId = Guid.NewGuid();
        var password = "password";
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = true,
            Password = PasswordHasher.HashPassword(password)
        };
        var req = new UserLoginRequest(
            Email: user.Email,
            Password: password
        );
        var mockAccessToken = "mockAccessToken";
        var mockRefreshToken = Guid.NewGuid();
        _tokenServiceMock.Setup(t => t.GenerateAccessToken(user.Name, user.Email, user.Sub)).Returns(mockAccessToken);
        _tokenServiceMock.Setup(t => t.GenerateRefreshToken()).Returns(mockRefreshToken);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var actual = await _userService.Login(req);
        _redisServiceMock.Verify(r => r.SetUserSubAsync(user.Sub, It.IsAny<UserSubHash>(), It.IsAny<TimeSpan>()), Times.Once);
        _redisServiceMock.Verify(r => r.SetRefreshToken(mockRefreshToken, It.IsAny<RefreshTokenHash>(), It.IsAny<DateTime>()), Times.Once);
        Assert.Equal(mockRefreshToken, actual.RefreshToken);
        Assert.Equal(mockAccessToken, actual.AccessToken);
    }
    // RotateRefreshToken

    // VerifyEmail
    [Fact]
    public async Task VerifyEmail_UserIdIsNull()
    {
        Guid token = Guid.NewGuid();
        _redisServiceMock.Setup(r => r.GetEmailVerifyUserId(token)).ReturnsAsync((Guid?)null);
        var actual = await _userService.VerifyEmail(token);
        Assert.False(actual);
        _redisServiceMock.Verify(r => r.GetEmailVerifyUserId(token), Times.Once);
    }
    [Fact]
    public async Task VerifyEmail_UserIsNull()
    {
        Guid userId = Guid.NewGuid();
        Guid token = Guid.NewGuid();
        _redisServiceMock.Setup(r => r.GetEmailVerifyUserId(token)).ReturnsAsync(userId);
        var actual = await _userService.VerifyEmail(token);
        Assert.False(actual);
        _redisServiceMock.Verify(r => r.GetEmailVerifyUserId(token), Times.Once);
        _redisServiceMock.Verify(r => r.DeleteEmailVerifyTokenAsync(token), Times.Never);
        var count = await _dbContext.Users.CountAsync();
        Assert.Equal(0, count);
    }
    [Fact]
    public async Task VerifyEmail_Success()
    {
        var userId = Guid.NewGuid();
        var token = Guid.NewGuid();
        _redisServiceMock.Setup(r => r.GetEmailVerifyUserId(token)).ReturnsAsync(userId);
        var req = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            Password = "password",
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = false
        };
        _dbContext.Users.Add(req);
        await _dbContext.SaveChangesAsync();
        var actual = await _userService.VerifyEmail(token);
        Assert.True(actual);
        _redisServiceMock.Verify(r => r.GetEmailVerifyUserId(token), Times.Once);
        _redisServiceMock.Verify(r => r.DeleteEmailVerifyTokenAsync(token), Times.Once);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        Assert.NotNull(user);
        Assert.True(user.IsEmailVerified);
    }
    // GetProfile
    [Fact]
    public async Task GetProfile_UserNotFound()
    {
        var userId = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await _userService.GetProfile(userId));
        Assert.Equal("使用者不存在", exception.Message);
    }
    [Fact]
    public async Task GetProfile_Success()
    {
        var userId = Guid.NewGuid();
        var req = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            Password = "password",
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = true
        };
        _dbContext.Users.Add(req);
        await _dbContext.SaveChangesAsync();
        var actual = await _userService.GetProfile(userId);
        Assert.NotNull(actual);
        Assert.Equal(req.Name, actual.Name);
        Assert.Equal(req.Email, actual.Email);
        Assert.Equal((int)req.AuthProvider, actual.AuthProvider);
        Assert.Equal(req.IsEmailVerified, actual.IsEmailVerified);
    }
    // UploadAvatar
    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/bmp")]
    [InlineData("application/pdf")]
    public async Task UploadAvatar_FormatError(string fileFormat)
    {
        var format = fileFormat.Split("/").Last();
        IFormFile file = new FormFile(Stream.Null, 0, 0, null, $"test.{format}")
        {
            Headers = new HeaderDictionary(),
            ContentType = fileFormat
        };
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await _userService.UploadAvatar(file, Guid.NewGuid()));
        Assert.Equal("僅支援 jpg、png、webp 格式", exception.Message);
    }
    [Theory]
    [InlineData(10 * 1024 * 1024 + 1)]
    [InlineData(10 * 1024 * 1024 + 10)]
    [InlineData(10 * 1024 * 1024 + 20)]
    public async Task UploadAvatar_SizeError(long fileSize)
    {
        IFormFile file = new FormFile(Stream.Null, 0, fileSize, null, "test.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await _userService.UploadAvatar(file, Guid.NewGuid()));
        Assert.Equal("檔案大小不可超過 10MB", exception.Message);
    }
    [Fact]
    public async Task UploadAvatar_SuccessHasOldAvatar()
    {
        var userId = Guid.NewGuid();
        var newOriginalPath = "newOriginalPath";
        var newThumbPath = "newThumbPath";
        IFormFile file = new FormFile(Stream.Null, 0, 1024, null, "test.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        _fileStorageServiceMock.Setup(f => f.UploadAvatarAsync(file)).ReturnsAsync((newOriginalPath, newThumbPath));
        var avatar = new Avatar
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "oldOriginalPath",
            ThumbUrl = "oldThumbPath",
            UserId = userId
        };
        _dbContext.Avatars.Add(avatar);
        await _dbContext.SaveChangesAsync();
        await _userService.UploadAvatar(file, userId);
        _fileStorageServiceMock.Verify(f => f.UploadAvatarAsync(file), Times.Once);
        _fileStorageServiceMock.Verify(f => f.DeleteAsync(avatar.OriginalUrl), Times.Once);
        _fileStorageServiceMock.Verify(f => f.DeleteAsync(avatar.ThumbUrl), Times.Once);
        var newAvatar = await _dbContext.Avatars.FirstOrDefaultAsync(a => a.UserId == userId);
        Assert.NotNull(newAvatar);
        Assert.Equal(newOriginalPath, newAvatar.OriginalUrl);
        Assert.Equal(newThumbPath, newAvatar.ThumbUrl);
    }
    [Fact]
    public async Task UploadAvatar_SuccessNotHasOldAvatar()
    {
        var userId = Guid.NewGuid();
        var newOriginalPath = "newOriginalPath";
        var newThumbPath = "newThumbPath";
        IFormFile file = new FormFile(Stream.Null, 0, 1024, null, "test.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        _fileStorageServiceMock.Setup(f => f.UploadAvatarAsync(file)).ReturnsAsync((newOriginalPath, newThumbPath));

        await _userService.UploadAvatar(file, userId);
        _fileStorageServiceMock.Verify(f => f.UploadAvatarAsync(file), Times.Once);
        _fileStorageServiceMock.Verify(f => f.DeleteAsync(It.IsAny<string>()), Times.Never);
        _fileStorageServiceMock.Verify(f => f.DeleteAsync(It.IsAny<string>()), Times.Never);
        var newAvatar = await _dbContext.Avatars.FirstOrDefaultAsync(a => a.UserId == userId);
        Assert.NotNull(newAvatar);
        Assert.Equal(newOriginalPath, newAvatar.OriginalUrl);
        Assert.Equal(newThumbPath, newAvatar.ThumbUrl);
    }
    // ForgotPassword
    [Fact]
    public async Task ForgetPassword_UserNotFound()
    {
        await _userService.ForgetPassword("test@test.com");
        _redisServiceMock.Verify(r => r.GetForgetPasswordCooldown(It.IsAny<Guid>()), Times.Never);
    }
    public static IEnumerable<object[]> UserData1 =>
    [
        [new User { Id = Guid.NewGuid(), Name = "Test1", Email = "test1@test1.com", AuthProvider = AuthProviderEnum.Google, IsEmailVerified = true}],
        [new User { Id = Guid.NewGuid(), Name = "Test2", Email = "test2@test2.com", AuthProvider = AuthProviderEnum.Local, IsEmailVerified = false}]
    ];
    [Theory]
    [MemberData(nameof(UserData1))]
    public async Task ForgetPassword_UserStatusError(User user)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _userService.ForgetPassword(user.Email);
        _redisServiceMock.Verify(r => r.GetForgetPasswordCooldown(It.IsAny<Guid>()), Times.Never);
    }
    [Fact]
    public async Task ForgetPassword_CooldownIsExist()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            Password = PasswordHasher.HashPassword("testPassword"),
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = true
        };
        _redisServiceMock.Setup(r => r.GetForgetPasswordCooldown(userId)).ReturnsAsync(true);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _userService.ForgetPassword(user.Email);
        _redisServiceMock.Verify(r => r.GetForgetPasswordCooldown(userId), Times.Once);
        _redisServiceMock.Verify(r => r.SetForgetPasswordToken(It.IsAny<Guid>(), userId, It.IsAny<TimeSpan>()), Times.Never);
        _redisServiceMock.Verify(r => r.SetForgetPasswordCooldown(userId, It.IsAny<TimeSpan>()), Times.Never);
    }
    [Fact]
    public async Task ForgetPassword_Success()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            Password = PasswordHasher.HashPassword("testPassword"),
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = true
        };
        _redisServiceMock.Setup(r => r.GetForgetPasswordCooldown(userId)).ReturnsAsync(false);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _userService.ForgetPassword(user.Email);
        _redisServiceMock.Verify(r => r.GetForgetPasswordCooldown(userId), Times.Once);
        _redisServiceMock.Verify(r => r.SetForgetPasswordToken(It.IsAny<Guid>(), userId, It.IsAny<TimeSpan>()), Times.Once);
        _redisServiceMock.Verify(r => r.SetForgetPasswordCooldown(userId, It.IsAny<TimeSpan>()), Times.Once);
        _emailServiceMock.Verify(e => e.SendAsync(user.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
    // ResetPassword
    [Fact]
    public async Task ResetPassword_TokenInvalid()
    {
        Guid token = Guid.NewGuid();
        _redisServiceMock.Setup(r => r.GetForgetPasswordUserId(token)).ReturnsAsync((Guid?)null);
        var req = new UserResetPasswordRequest(
            Password: "newPassword",
            Token: token
        );
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await _userService.ResetPassword(req));
        Assert.Equal("連結已失效，請重新申請", exception.Message);
    }
    [Fact]
    public async Task ResetPassword_UserNotFound()
    {
        Guid token = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _redisServiceMock.Setup(r => r.GetForgetPasswordUserId(token)).ReturnsAsync(userId);
        var req = new UserResetPasswordRequest(
            Password: "newPassword",
            Token: token
        );
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await _userService.ResetPassword(req));
        Assert.Equal("連結已失效，請重新申請", exception.Message);
    }
    [Fact]
    public async Task ResetPassword_Success()
    {
        var userId = Guid.NewGuid();
        var token = Guid.NewGuid();
        _redisServiceMock.Setup(r => r.GetForgetPasswordUserId(token)).ReturnsAsync(userId);
        _redisServiceMock.Setup(r => r.GetUserAllRefreshToken(userId)).ReturnsAsync(new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            Password = PasswordHasher.HashPassword("testPassword"),
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = true
        };
        var req = new UserResetPasswordRequest(
            Password: "newPassword",
            Token: token
        );
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _userService.ResetPassword(req);
        _redisServiceMock.Verify(r => r.GetForgetPasswordUserId(token), Times.Once);
        _redisServiceMock.Verify(r => r.GetUserAllRefreshToken(userId), Times.Once);
        _redisServiceMock.Verify(r => r.DeleteRefreshToken(It.IsAny<Guid>()), Times.Exactly(3));
        _redisServiceMock.Verify(r => r.DeleteForgetPasswordToken(token), Times.Once);
        _redisServiceMock.Verify(r => r.DeleteUserRefreshToken(userId), Times.Once);
        var actual = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        Assert.NotNull(actual);
        Assert.True(PasswordHasher.VerifyPassword(req.Password, actual.Password));
    }
    // ChangePassword
    [Fact]
    public async Task ChangePassword_UserNotFound()
    {
        var userId = Guid.NewGuid();
        var req = new UserChangePasswordRequest(
            OldPassword: "oldPassword",
            NewPassword: "newPassword"
        );
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await _userService.ChangePassword(req, userId));
        Assert.Equal("使用者不存在", exception.Message);
    }
    [Fact]
    public async Task ChangePassword_UserBoundGoogle()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            Password = null,
            AuthProvider = AuthProviderEnum.Google,
            IsEmailVerified = true
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var req = new UserChangePasswordRequest(
            OldPassword: "oldPassword",
            NewPassword: "newPassword"
        );
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await _userService.ChangePassword(req, userId));
        Assert.Equal("該帳號已綁定 Google，無法修改密碼", exception.Message);
    }
    [Fact]
    public async Task ChangePassword_OldPsswordIncorrect()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            Password = PasswordHasher.HashPassword("testPassword"),
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = true
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var req = new UserChangePasswordRequest(
            OldPassword: "oldPassword",
            NewPassword: "newPassword"
        );
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await _userService.ChangePassword(req, userId));
        Assert.Equal("舊密碼錯誤", exception.Message);
    }
    [Fact]
    public async Task ChangePassword_Success()
    {
        var userId = Guid.NewGuid();
        var oldPassword = "oldPassword";
        _redisServiceMock.Setup(r => r.GetUserAllRefreshToken(userId)).ReturnsAsync(new List<Guid>() { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });

        var user = new User
        {
            Id = userId,
            Name = "test",
            Email = "test@test.com",
            Password = PasswordHasher.HashPassword(oldPassword),
            AuthProvider = AuthProviderEnum.Local,
            IsEmailVerified = true
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var req = new UserChangePasswordRequest(
            OldPassword: oldPassword,
            NewPassword: "newPassword"
        );
        await _userService.ChangePassword(req, userId);
        _redisServiceMock.Verify(r => r.GetUserAllRefreshToken(userId), Times.Once);
        _redisServiceMock.Verify(r => r.DeleteRefreshToken(It.IsAny<Guid>()), Times.Exactly(3));
        _redisServiceMock.Verify(r => r.DeleteUserRefreshToken(userId), Times.Once);
        var actual = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        Assert.NotNull(actual?.Password);
        Assert.True(PasswordHasher.VerifyPassword(req.NewPassword, actual.Password));
    }
}

using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Bill_App_API.Utils;
using Bill_App_API.Options;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Bill_App_API.Services;

public class UserService(BillDbContext dbContext, IRedisService redisService, ITokenService tokenService, IOptions<MaxDeviceOptions> maxDeviceOptions, IOptions<RefreshTokenOptions> refreshTokenOptions, IOptions<UserCacheOptions> userCacheOptions) : IUserService
{
    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;
    private readonly UserCacheOptions _userCacheOptions = userCacheOptions.Value;
    public async Task<bool> Signup(UserSignupRequest req)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == req.Email);
        if (user is null)
        {
            var hashPassword = PasswordHasher.HashPassword(req.Password);
            User newUser = new User
            {
                Name = req.Name,
                Email = req.Email,
                Password = hashPassword
            };
            await dbContext.Users.AddAsync(newUser);
            await dbContext.SaveChangesAsync();
            return true;

        }
        return false;
    }
    public async Task Logout(string refreshToken)
    {
        Guid refreshTokenGuid;
        bool isTransformCorrect = Guid.TryParse(refreshToken, out refreshTokenGuid);
        if(!isTransformCorrect)
        {
            return;
        }
        var uesrHash = await redisService.GetRefreshToken(refreshTokenGuid);
        if(uesrHash is null)
        {
            return;
        }
        await redisService.DeleteUserRefreshTokenByMember(uesrHash.UserId, refreshTokenGuid);
        await redisService.DeleteRefreshToken(refreshTokenGuid);
        return;
    }
    public async Task<UserLoginResponse?> Login(UserLoginRequest req)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == req.Email);
        if (user is not null)
        {
            bool isVerify = PasswordHasher.VerifyPassword(req.Password, user.Password);
            if (isVerify)
            {
                var userSub = new UserSubHash(
                    UserId: user.Id,
                    Email: user.Email,
                    Name: user.Name
                );
                
                // �ͦ�redis��user sub hash��T & zset �A�L���ɶ���7��
                var expireAt = DateTime.UtcNow.AddDays(_refreshTokenOptions.DurationInDay);
                // �ͦ�access token
                var accessToken = tokenService.GenerateAccessToken(user.Name, user.Email, user.Sub);
                // �إ�redis��user sub hash��T
                await redisService.SetUserSubAsync(user.Sub, userSub, TimeSpan.FromHours(_userCacheOptions.TtlInHours));
                // ����/���� Refresh Token�A�è��o�s��Refresh Token
                var refreshToken = await RotateRefreshToken(user.Id, expireAt);
                // �إ�redis��user hash��T
                await redisService.SetRefreshToken(refreshToken, new RefreshTokenHash(
                    UserId: user.Id,
                    Email: user.Email,
                    Expire: expireAt.ToString("o"),
                    Sub: user.Sub,
                    Name: user.Name,
                    IsOld: IsOldType.No
                ), expireAt);
                return new UserLoginResponse(
                    AccessToken: accessToken,
                    RefreshToken: refreshToken
                );
            }
        }
        return null;
    }
    public async Task<Guid> RotateRefreshToken(Guid userId, DateTime expireAt)
    {
        // �ͦ�refresh token
        var refreshToken = tokenService.GenerateRefreshToken();
        // �إ�redis��refresh token zset
        var maxDevice = maxDeviceOptions.Value.MaxDevice;
        // �R���s�bZset�w�L����Refresh Token
        await redisService.DeleteExpiredUserRefreshTokens(userId);
        // �d��Zset�ثe������
        var count = await redisService.GetUserRefreshTokenCount(userId);
        // �p�GZset���ƶq�j�󵥩�̤j�˸m�ƶq�N�R�����ª��@��Refresh Token Hash�������]�n�R��
        if (count >= maxDevice)
        {
            var oldRefreshToken = await redisService.PopOldestUserRefreshToken(userId);
            if (oldRefreshToken is not null)
            {
                await redisService.DeleteRefreshToken(Guid.Parse(oldRefreshToken));
            }
            else
            {
                // �p�G�S��������ª�Refresh Token�A�N�������`�A�o��i�H��ܬ���log�άO��L�B�z�覡

            }
        }
        // �]�m�s��Refresh Token Zset
        await redisService.SetUserRefreshToken(userId, refreshToken, expireAt);
        return refreshToken;
    }
    public async Task<Guid?> GetUserId(Guid sub)
    {
        var result = await redisService.GetUserSubAsync(sub);
        if (result is null)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Sub == sub);
            if (user is null)
            {
                return null;
            }
            return user.Id;
        }
        return result.UserId;
    }
}


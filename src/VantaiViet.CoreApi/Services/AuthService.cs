using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class AuthService(
    CoreDbContext dbContext,
    UserManager<User> userManager,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ICurrentActor actor) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var phone = request.PhoneNumber.Trim();
        if (await userManager.FindByNameAsync(phone) is not null)
        {
            throw new InvalidOperationException("Phone number is already registered.");
        }

        var now = timeProvider.GetUtcNow();
        var registration = new RegistrationApplication
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            PhoneVerifiedAt = null,
            DisplayName = request.DisplayName.Trim(),
            Status = "Completed",
            ExpiresAt = now.AddDays(1),
            CompletedAt = now
        };
        dbContext.RegistrationApplications.Add(registration);
        var user = new User
        {
            RegistrationApplicationId = registration.Id,
            DisplayName = registration.DisplayName,
            AccountStatus = "PendingKyc",
            UserName = phone,
            PhoneNumber = phone,
            PhoneNumberConfirmed = false
        };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(x => x.Description)));
        }

        var addUserRole = await userManager.AddToRoleAsync(user, "User");
        var addAccountTypeRole = await userManager.AddToRoleAsync(user, request.AccountType);
        if (!addUserRole.Succeeded || !addAccountTypeRole.Succeeded)
        {
            throw new InvalidOperationException("Unable to assign the selected account type.");
        }
        var response = await CreateResponseAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var phone = request.PhoneNumber.Trim().ToUpperInvariant();
        var user = await dbContext.Users.FromSqlInterpolated($"""SELECT * FROM public."Users" WHERE "NormalizedUserName"={phone} FOR UPDATE""").SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid phone number or password.");
        if (await userManager.IsLockedOutAsync(user)) { throw new UnauthorizedAccessException("Invalid phone number or password."); }
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid phone number or password.");
        }
        if (user.AccountStatus is "Suspended" or "Disabled")
        {
            throw new UnauthorizedAccessException("This account cannot sign in.");
        }
        await userManager.ResetAccessFailedCountAsync(user);
        var response = await CreateResponseAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<ShipmentResult<AuthResponse>> RefreshAsync(RefreshSessionRequest request,CancellationToken cancellationToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken));
        var userId = await dbContext.AuthSessions.Where(x=>x.RefreshTokenHash==hash).Select(x=>(Guid?)x.UserId).SingleOrDefaultAsync(cancellationToken);
        if (userId is null) { return ShipmentResult<AuthResponse>.Fail("invalid_credentials"); }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId.Value,cancellationToken);
        var session = await dbContext.AuthSessions.SingleAsync(x=>x.RefreshTokenHash==hash,cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (session.RevokedAt is not null)
        {
            await dbContext.AuthSessions.Where(x=>x.FamilyId==session.FamilyId && x.RevokedAt==null)
                .ExecuteUpdateAsync(x=>x.SetProperty(s=>s.RevokedAt,now).SetProperty(s=>s.RevocationReason,"refresh_reuse"),cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ShipmentResult<AuthResponse>.Fail("invalid_credentials");
        }
        if (session.ExpiresAt<=now || user.AccountStatus is "Suspended" or "Disabled" || await userManager.IsLockedOutAsync(user))
        { return ShipmentResult<AuthResponse>.Fail("invalid_credentials"); }
        session.RevokedAt = now;
        session.RevocationReason = "rotated";
        var response = await CreateResponseAsync(user,cancellationToken,session.FamilyId);
        await transaction.CommitAsync(cancellationToken);
        return new(response);
    }

    public Task<ShipmentResult<bool>> LogoutAsync(CancellationToken cancellationToken) => RevokeSessionAsync(actor.SessionId,cancellationToken);

    public async Task<ShipmentResult<bool>> RevokeSessionAsync(Guid sessionId,CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(actor.UserId,cancellationToken);
        var family = await dbContext.AuthSessions.Where(x=>x.Id==sessionId && x.UserId==actor.UserId).Select(x=>(Guid?)x.FamilyId).SingleOrDefaultAsync(cancellationToken);
        if (family is null) { return ShipmentResult<bool>.Fail("not_found"); }
        await dbContext.AuthSessions.Where(x=>x.UserId==actor.UserId && x.FamilyId==family && x.RevokedAt==null)
            .ExecuteUpdateAsync(x=>x.SetProperty(s=>s.RevokedAt,timeProvider.GetUtcNow()).SetProperty(s=>s.RevocationReason,"logout"),cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true);
    }

    public async Task<ShipmentResult<IReadOnlyList<AuthSessionResponse>>> SessionsAsync(CancellationToken cancellationToken) =>
        new(await dbContext.AuthSessions.AsNoTracking().Where(x=>x.UserId==actor.UserId && x.RevokedAt==null && x.ExpiresAt>timeProvider.GetUtcNow())
            .OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).Take(100)
            .Select(x=>new AuthSessionResponse(x.Id,x.CreatedAt,x.ExpiresAt,x.Id==actor.SessionId)).ToListAsync(cancellationToken));

    public async Task<ShipmentResult<bool>> ChangePasswordAsync(ChangePasswordRequest request,CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(actor.UserId,cancellationToken);
        if (user.AccountStatus is "Suspended" or "Disabled" || await userManager.IsLockedOutAsync(user)) { return ShipmentResult<bool>.Fail("invalid_credentials"); }
        if (!await userManager.CheckPasswordAsync(user,request.CurrentPassword))
        {
            await userManager.AccessFailedAsync(user);
            await transaction.CommitAsync(cancellationToken);
            return ShipmentResult<bool>.Fail("invalid_credentials");
        }
        var changed = await userManager.ChangePasswordAsync(user,request.CurrentPassword,request.NewPassword);
        if (!changed.Succeeded) { return ShipmentResult<bool>.Fail("invalid_password"); }
        await dbContext.AuthSessions.Where(x=>x.UserId==user.Id && x.RevokedAt==null)
            .ExecuteUpdateAsync(x=>x.SetProperty(s=>s.RevokedAt,timeProvider.GetUtcNow()).SetProperty(s=>s.RevocationReason,"password_changed"),cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true);
    }

    public Task<bool> IsSessionActiveAsync(Guid userId,Guid sessionId,string securityStamp,CancellationToken cancellationToken) =>
        (from session in dbContext.AuthSessions join user in dbContext.Users on session.UserId equals user.Id
         where session.Id==sessionId && user.Id==userId && session.RevokedAt==null && session.ExpiresAt>timeProvider.GetUtcNow()
            && user.SecurityStamp==securityStamp && (user.AccountStatus=="Active" || user.AccountStatus=="PendingKyc")
         select session.Id).AnyAsync(cancellationToken);

    private Task<User> LockUserAsync(Guid id,CancellationToken cancellationToken) =>
        dbContext.Users.FromSqlInterpolated($"""SELECT * FROM public."Users" WHERE "Id"={id} FOR UPDATE""").SingleAsync(cancellationToken);

    private async Task<AuthResponse> CreateResponseAsync(User user, CancellationToken cancellationToken,Guid? familyId = null)
    {
        var now = timeProvider.GetUtcNow();
        var accessExpiresAt = now.AddMinutes(30);
        var refreshExpiresAt = now.AddDays(14);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId ?? Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = now,
            RefreshTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)),
            ExpiresAt = refreshExpiresAt
        };
        dbContext.AuthSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        var key = configuration["Authentication:JwtSigningKey"]
            ?? throw new InvalidOperationException("Configure Authentication:JwtSigningKey.");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("sid",session.Id.ToString()),
            new("security_stamp",user.SecurityStamp ?? throw new InvalidOperationException("Missing security stamp.")),
            new(ClaimTypes.Name, user.UserName ?? user.Id.ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var token = new JwtSecurityToken(
            issuer: configuration["Authentication:Issuer"],
            audience: configuration["Authentication:Audience"],
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExpiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        return new AuthResponse(
            user.Id,
            user.DisplayName,
            user.AccountStatus,
            roles.Order().ToList(),
            true,
            new JwtSecurityTokenHandler().WriteToken(token),
            accessExpiresAt,
            refreshToken,
            refreshExpiresAt);
    }
}

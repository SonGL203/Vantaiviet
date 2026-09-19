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
    TimeProvider timeProvider) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
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
            PhoneVerifiedAt = now,
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
            PhoneNumberConfirmed = true
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
        return await CreateResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(request.PhoneNumber.Trim())
            ?? throw new UnauthorizedAccessException("Invalid phone number or password.");
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException("Invalid phone number or password.");
        }
        if (user.AccountStatus is "Suspended" or "Disabled")
        {
            throw new UnauthorizedAccessException("This account cannot sign in.");
        }
        return await CreateResponseAsync(user, cancellationToken);
    }

    private async Task<AuthResponse> CreateResponseAsync(User user, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var accessExpiresAt = now.AddMinutes(30);
        var refreshExpiresAt = now.AddDays(14);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        dbContext.AuthSessions.Add(new AuthSession
        {
            UserId = user.Id,
            RefreshTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)),
            ExpiresAt = refreshExpiresAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var key = configuration["Authentication:JwtSigningKey"]
            ?? throw new InvalidOperationException("Configure Authentication:JwtSigningKey.");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!)
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

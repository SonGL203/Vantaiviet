using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class RegistrationService(CoreDbContext dbContext, TimeProvider timeProvider)
    : IRegistrationService
{
    public async Task<RegistrationCreatedResponse> CreateAsync(
        CreateRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var phoneNumber = request.PhoneNumber.Trim();
        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.RegistrationApplications
            .AnyAsync(x => x.PhoneNumber == phoneNumber && x.Status == "InProgress", cancellationToken);
        if (existing)
        {
            throw new InvalidOperationException("An active registration already exists for this phone number.");
        }

        var token = RandomNumberGenerator.GetBytes(32);
        var application = new RegistrationApplication
        {
            PhoneNumber = phoneNumber,
            DisplayName = request.DisplayName?.Trim(),
            ExpiresAt = now.AddHours(24)
        };
        dbContext.RegistrationApplications.Add(application);
        dbContext.RegistrationSessions.Add(new RegistrationSession
        {
            RegistrationApplicationId = application.Id,
            TokenHash = SHA256.HashData(token),
            ExpiresAt = now.AddHours(24)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RegistrationCreatedResponse(
            application.Id,
            Convert.ToBase64String(token),
            application.ExpiresAt);
    }
}

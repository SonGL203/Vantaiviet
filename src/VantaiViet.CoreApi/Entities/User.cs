using Microsoft.AspNetCore.Identity;

namespace VantaiViet.CoreApi.Entities;

public sealed class User : IdentityUser<Guid>
{
    public Guid RegistrationApplicationId { get; set; }
    public required string DisplayName { get; set; }
    public string AccountStatus { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The broker whose invite code this account registered with, feature 17 ("Mời tài xế vào app"). Null otherwise.</summary>
    public Guid? ReferredByUserId { get; set; }
}

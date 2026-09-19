using Microsoft.AspNetCore.Identity;

namespace VantaiViet.CoreApi.Entities;

public sealed class User : IdentityUser<Guid>
{
    public Guid RegistrationApplicationId { get; set; }
    public required string DisplayName { get; set; }
    public string AccountStatus { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

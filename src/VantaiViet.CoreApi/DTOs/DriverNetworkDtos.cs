using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

/// <summary>Feature 16: a broker's own named group of drivers.</summary>
public sealed record DriverGroupRequest([param: Required, StringLength(60, MinimumLength = 1)] string Name);

public sealed record DriverGroupResponse(Guid Id, string Name, int DriverCount, DateTimeOffset CreatedAt);

/// <summary>Feature 14: saving a driver the broker already knows of (from a vehicle-availability posting, for example).</summary>
public sealed record SaveDriverRequest([param: Required] Guid DriverUserId, Guid? GroupId, bool IsFavorite = false, [param: StringLength(500)] string? Note = null);

/// <summary>Features 15-16: moving a saved driver between groups, or marking them a favourite.</summary>
public sealed record UpdateSavedDriverRequest(Guid? GroupId, bool IsFavorite, [param: StringLength(500)] string? Note, [param: Range(1, long.MaxValue)] long Version);

public sealed record SavedDriverResponse(
    Guid Id, Guid DriverUserId, string DriverDisplayName, string DriverPhone,
    Guid? GroupId, string? GroupName, bool IsFavorite, string? Note,
    RatingSummary Rating, int CompletedTrips, long Version, DateTimeOffset CreatedAt);

/// <summary>Feature 17: the broker's own invite code, and who has joined through it.</summary>
public sealed record InviteCodeResponse(string Code);

public sealed record ReferredDriverResponse(Guid UserId, string DisplayName, string Phone, DateTimeOffset JoinedAt);

/// <summary>Feature 23: a broker flags a driver for admin attention (no moderation queue yet).</summary>
public sealed record DriverReportRequest(
    [param: Required] string Category,
    [param: Required, StringLength(1000, MinimumLength = 3)] string Description);

public sealed record DriverReportResponse(Guid Id, Guid DriverUserId, string DriverDisplayName, string Category, string Description, DateTimeOffset CreatedAt);

using System.ComponentModel.DataAnnotations;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.DTOs;

// ---- ratings

public sealed record CreateReviewRequest(Guid TargetUserId, [param: Range(1, 5)] int Stars,
    [param: StringLength(FollowUpLimits.MaxReviewComment)] string? Comment = null);

public sealed record ReviewInfo(int Stars, string? Comment, DateTimeOffset CreatedAt);

/// <summary>A carrier the owner may rate for this shipment and, once done, what they said.</summary>
public sealed record ReviewTarget(Guid UserId, string Kind, string DisplayName, ReviewInfo? Review);

/// <summary>CanReview is true only for the owner of a completed shipment; Targets lists who can be rated either way.</summary>
public sealed record ShipmentReviewsResponse(bool CanReview, IReadOnlyList<ReviewTarget> Targets);

public sealed record RatingSummary(double? Average, int Count);

public sealed record RecentReview(int Stars, string? Comment, DateTimeOffset CreatedAt);

// ---- incidents

public sealed record ReportIncidentRequest([param: Required, StringLength(20)] string Category,
    [param: Required, StringLength(FollowUpLimits.MaxIncidentLength, MinimumLength = FollowUpLimits.MinIncidentLength)] string Description);

public sealed record IncidentResponse(Guid Id, string Category, string Description, string Status, DateTimeOffset CreatedAt);

// ---- notifications

public sealed record NotificationResponse(long Id, string Type, Guid? ShipmentId, Guid? RelatedUserId, string? Subject,
    string? ActorName, DateTimeOffset CreatedAt, bool Read);

public sealed record NotificationPage(IReadOnlyList<NotificationResponse> Items, int UnreadCount, bool HasMore);

public sealed record MarkNotificationsRequest([param: Required, MinLength(1), MaxLength(100)] long[] Ids);

public sealed record UnreadCountResponse(int Count);

// ---- history

/// <summary>One line of an order's log. Action is a code such as "shipment.created" or "trip.deliver"; clients word it.
/// Detail is the short value that goes with some actions: an incident's category, a rating's star count.</summary>
public sealed record HistoryEntry(DateTimeOffset At, string Action, string? ActorName, bool ByMe, string? Note, string? Detail = null);

public sealed record ShipmentHistoryResponse(string Code, string Status, IReadOnlyList<HistoryEntry> Entries);

// ---- saved addresses

public sealed record SaveAddressRequest(
    [param: Required, StringLength(FollowUpLimits.MaxAddressLabel)] string Label,
    [param: Required, StringLength(FollowUpLimits.MaxAddressLength)] string Address,
    CoordinateDto? Point = null,
    [param: Range(1, long.MaxValue)] long? Version = null);

public sealed record SavedAddressResponse(Guid Id, string Label, string Address, CoordinateDto? Point, long Version);

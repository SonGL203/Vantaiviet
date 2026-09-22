namespace VantaiViet.CoreApi.Entities;

/// <summary>The owner's rating of one carrier (the trip's driver, or the broker who held the job) once the trip is completed.</summary>
public sealed class TripReview
{
    public Guid TripId { get; set; }
    public Guid TargetUserId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A problem the shipper reports on a booked shipment. It is kept for support to follow up; the driver is told.</summary>
public sealed class ShipmentIncident
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid ReporterUserId { get; set; }
    public required string Category { get; set; }
    public required string Description { get; set; }
    public string Status { get; set; } = IncidentStatuses.Open;
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Something that happened to a shipment that a user should hear about. Clients turn <see cref="Type"/> into words.</summary>
public sealed class UserNotification
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public required string Type { get; set; }
    public Guid? ShipmentId { get; set; }

    // The other person involved (who wrote, who asked); chat messages from one person share one unread notification.
    public Guid? RelatedUserId { get; set; }

    // The shipment code and the actor's name as they were, so the text still reads right after either changes.
    public string? Subject { get; set; }
    public string? ActorName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>A place the shipper picks up from or delivers to often.</summary>
public sealed class SavedAddress
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Label { get; set; }
    public required string Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class IncidentStatuses
{
    public const string Open = "Open";
}

public static class IncidentCategories
{
    public const string DamagedCargo = "DamagedCargo";
    public const string MissingCargo = "MissingCargo";
    public const string Late = "Late";
    public const string DriverConduct = "DriverConduct";
    public const string WrongDelivery = "WrongDelivery";
    public const string Accident = "Accident";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [DamagedCargo, MissingCargo, Late, DriverConduct, WrongDelivery, Accident, Other];
}

public static class NotificationTypes
{
    public const string RequestReceived = "request.received";
    public const string RequestWithdrawn = "request.withdrawn";
    public const string RequestAccepted = "request.accepted";
    public const string RequestRejected = "request.rejected";
    public const string BrokerAssigned = "broker.assigned";
    public const string BrokerAccepted = "broker.accepted";
    public const string BrokerRejected = "broker.rejected";
    public const string BrokerRevoked = "broker.revoked";
    public const string TripStarted = "trip.start";
    public const string TripPickedUp = "trip.pickup";
    public const string TripDelivered = "trip.deliver";
    public const string TripCompleted = "trip.complete";
    public const string TripCancelled = "trip.cancel";
    public const string TripIncident = "trip.incident";
    public const string ShipmentCancelled = "shipment.cancelled";
    public const string ChatMessage = "chat.message";
    public const string ReviewReceived = "review.received";
    public const string IncidentReported = "incident.reported";
    // Features 13/18-20: a broker's price offer on a driver's open truck, and its answer either way.
    public const string ProposalReceived = "vehicle_proposal.received";
    public const string ProposalCountered = "vehicle_proposal.countered";
    public const string ProposalAccepted = "vehicle_proposal.accepted";
    public const string ProposalRejected = "vehicle_proposal.rejected";
    public const string ProposalWithdrawn = "vehicle_proposal.withdrawn";
    // Feature 25 ("Có xe phù hợp"): a driver the broker has saved just posted a truck looking for cargo.
    public const string SavedDriverAvailable = "saved_driver.available";
    // "Có hàng phù hợp": a new shipment was just published on a route the broker watches.
    public const string MatchingCargoAvailable = "cargo.available";
}

public static class FollowUpLimits
{
    public const int MaxReviewComment = 500;
    public const int MinIncidentLength = 3;
    public const int MaxIncidentLength = 1000;
    public const int MaxOpenIncidents = 20;
    public const int MaxSavedAddresses = 30;
    public const int MaxAddressLabel = 60;
    public const int MaxAddressLength = 500;
    public const int NotificationPageSize = 30;
    public const int RecentReviews = 5;
    public const int MaxRouteInterests = 20;
}

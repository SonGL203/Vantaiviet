using System.ComponentModel.DataAnnotations;
namespace VantaiViet.CoreApi.DTOs;
public sealed record TripVersionRequest([param: Range(1, long.MaxValue)] long Version);
public sealed record TripHandoverRequest([param: Range(1, long.MaxValue)] long Version, Guid ProofId,
    [param: StringLength(1000)] string? Note);
public sealed record TripReasonRequest([param: Range(1, long.MaxValue)] long Version,
    [param: Required, StringLength(1000, MinimumLength = 3)] string Reason);
public sealed record UploadTripProofRequest([param: Required] string ContentType,
    [param: Required, MinLength(8), MaxLength(5242880)] byte[] Content);
public sealed record TripProofResponse(Guid Id, string ContentType, int SizeBytes, DateTimeOffset CreatedAt, long TripVersion);
public sealed record TripProofFile(string ContentType, byte[] Content);
public sealed record TripResponse(Guid Id, Guid BookingId, Guid DriverUserId, Guid VehicleId, string Status, long Version,
    DateTimeOffset? StartedAt, DateTimeOffset? PickedUpAt, DateTimeOffset? DeliveredAt,
    DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt);
public sealed record TripEventResponse(Guid Id, Guid ActorUserId, string Action, string? Note, Guid? ProofId, DateTimeOffset OccurredAt);

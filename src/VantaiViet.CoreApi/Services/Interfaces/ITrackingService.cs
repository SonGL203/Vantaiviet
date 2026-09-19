using VantaiViet.CoreApi.DTOs;
namespace VantaiViet.CoreApi.Services.Interfaces;
public interface ITrackingService
{
    Task<bool> CanViewAsync(Guid tripId,Guid authenticatedUserId,CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<TrackingTripResponse>>> ListAsync(int page,CancellationToken cancellationToken);
    Task<ShipmentResult<TrackingSnapshot>> SnapshotAsync(Guid id,CancellationToken cancellationToken);
    Task<ShipmentResult<GpsBatchResponse>> SendAsync(Guid id,GpsBatchRequest request,CancellationToken cancellationToken);
    Task<ShipmentResult<GpsHistoryResponse>> HistoryAsync(Guid id,long afterId,CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> SetStopsAsync(Guid id,SetTripStopsRequest request,CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<TrackingParticipantResponse>>> ParticipantsAsync(Guid id,CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> SetParticipantAsync(Guid id,Guid userId,bool allow,CancellationToken cancellationToken);
    Task<ShipmentResult<RouteResponse>> RouteAsync(Guid id,CancellationToken cancellationToken);
}

using VantaiViet.CoreApi.DTOs;
namespace VantaiViet.CoreApi.Services.Interfaces;
public interface ITripService
{
    Task<ShipmentResult<TripResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<TripEventResponse>>> EventsAsync(Guid id, int page, CancellationToken cancellationToken);
    Task<ShipmentResult<TripResponse>> StartAsync(Guid id, TripVersionRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<TripResponse>> PickupAsync(Guid id, TripHandoverRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<TripResponse>> DeliverAsync(Guid id, TripHandoverRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<TripResponse>> CompleteAsync(Guid id, TripVersionRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<TripResponse>> CancelAsync(Guid id, TripReasonRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<TripResponse>> ReportIncidentAsync(Guid id, TripReasonRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<TripProofResponse>> UploadProofAsync(Guid id, UploadTripProofRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<TripProofFile>> DownloadProofAsync(Guid id, Guid proofId, CancellationToken cancellationToken);
}

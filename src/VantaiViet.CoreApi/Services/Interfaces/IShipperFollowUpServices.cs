using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IUserNotificationService
{
    Task<ShipmentResult<NotificationPage>> ListAsync(long beforeId, int pageSize, bool unreadOnly, CancellationToken cancellationToken);
    Task<ShipmentResult<UnreadCountResponse>> UnreadCountAsync(CancellationToken cancellationToken);

    /// <summary>Marks the given notifications read, or every unread one when <paramref name="ids"/> is null.</summary>
    Task<ShipmentResult<UnreadCountResponse>> MarkReadAsync(IReadOnlyCollection<long>? ids, CancellationToken cancellationToken);
}

public interface IShipmentReviewService
{
    Task<ShipmentResult<ShipmentReviewsResponse>> ListAsync(Guid shipmentId, CancellationToken cancellationToken);
    Task<ShipmentResult<ReviewInfo>> CreateAsync(Guid shipmentId, CreateReviewRequest request, CancellationToken cancellationToken);
}

public interface IShipmentIncidentService
{
    Task<ShipmentResult<IReadOnlyList<IncidentResponse>>> ListAsync(Guid shipmentId, CancellationToken cancellationToken);
    Task<ShipmentResult<IncidentResponse>> ReportAsync(Guid shipmentId, ReportIncidentRequest request, CancellationToken cancellationToken);
}

public interface IShipmentHistoryService
{
    Task<ShipmentResult<ShipmentHistoryResponse>> HistoryAsync(Guid shipmentId, CancellationToken cancellationToken);
}

public interface ISavedAddressService
{
    Task<ShipmentResult<IReadOnlyList<SavedAddressResponse>>> ListAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<SavedAddressResponse>> CreateAsync(SaveAddressRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<SavedAddressResponse>> UpdateAsync(Guid id, SaveAddressRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<SavedAddressResponse>>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

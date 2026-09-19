using VantaiViet.CoreApi.DTOs;
namespace VantaiViet.CoreApi.Services.Interfaces;
public interface IBookingService
{
    Task<ShipmentResult<TransportRequestResponse>> RequestAsync(Guid shipmentId, RequestTransportDto request, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<TransportRequestResponse>>> ListRequestsAsync(Guid? shipmentId,int page,CancellationToken cancellationToken);
    Task<ShipmentResult<BookingResponse>> AcceptAsync(Guid requestId,CancellationToken cancellationToken);
    Task<ShipmentResult<TransportRequestResponse>> CloseRequestAsync(Guid requestId,bool withdraw,CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<BookingResponse>>> ListBookingsAsync(int page,CancellationToken cancellationToken);
}

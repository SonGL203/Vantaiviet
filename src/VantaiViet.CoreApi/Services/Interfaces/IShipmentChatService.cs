using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IShipmentChatService
{
    Task<ShipmentResult<ChatResponse>> ListAsync(Guid shipmentId, Guid? carrierUserId, long afterId, CancellationToken cancellationToken);
    Task<ShipmentResult<ChatMessageResponse>> SendAsync(Guid shipmentId, SendChatMessageRequest request, CancellationToken cancellationToken);
}

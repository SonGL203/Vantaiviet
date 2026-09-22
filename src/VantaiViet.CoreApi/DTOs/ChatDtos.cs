using System.ComponentModel.DataAnnotations;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.DTOs;

/// <summary>CarrierUserId names the other side of the conversation when the shipment owner writes; a carrier always talks to the owner.</summary>
public sealed record SendChatMessageRequest([param: Required, StringLength(ChatLimits.MaxBodyLength)] string Body, Guid? CarrierUserId = null);

public sealed record ChatMessageResponse(long Id, Guid SenderUserId, string Body, DateTimeOffset SentAt, bool Mine);

public sealed record ChatPartner(Guid UserId, string DisplayName, string? Phone);

public sealed record ChatResponse(IReadOnlyList<ChatMessageResponse> Messages, ChatPartner Partner, bool CanSend);

using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record AssignBrokerRequest(Guid BrokerUserId, [param: Range(1,long.MaxValue)] long Version);
public sealed record BrokerDecisionRequest([param: Range(1,long.MaxValue)] long Version);
public sealed record ShipmentBrokerResponse(Guid ShipmentId, string Title, Guid BrokerUserId, string Status, long Version);

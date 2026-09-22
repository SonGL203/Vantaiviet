using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record BrokerRouteInterestRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string FromArea,
    [param: Required, StringLength(200, MinimumLength = 1)] string ToArea);

public sealed record BrokerRouteInterestResponse(Guid Id, string FromArea, string ToArea, DateTimeOffset CreatedAt);

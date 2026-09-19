namespace VantaiViet.MatchingApi.DTOs;

public sealed record ServiceInfoResponse(
    string Service,
    string Version,
    DateTimeOffset TimestampUtc);

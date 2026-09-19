using System.ComponentModel.DataAnnotations;
namespace VantaiViet.CoreApi.DTOs;
public sealed record RequestOtpDto(Guid RequestKey,
    [param:Required,RegularExpression("^(Email|Phone)$")] string Channel,
    [param:Required,StringLength(256)] string Destination);
public sealed record VerifyOtpDto([param:Required,RegularExpression("^[0-9]{6}$")] string Code);
public sealed record OtpStatusDto(Guid Id,string Channel,string State,DateTimeOffset ExpiresAt,bool Simulated);
public sealed record OtpVerificationDto(bool Verified,bool Simulated);
public sealed record DevelopmentOtpDto(string Code,DateTimeOffset ExpiresAt);

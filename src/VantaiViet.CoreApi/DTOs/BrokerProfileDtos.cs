using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

/// <summary>Feature 22 ("Điểm uy tín cò xe"): Rating aggregates the shipper reviews left for this broker (as the carrier
/// who held a job), from the same TripReviews a shipper rates a driver with — nothing new to set up on the broker's side.</summary>
public sealed record BrokerProfileResponse(string OperatingArea, int YearsExperience, long Version, DateTimeOffset UpdatedAt, RatingSummary Rating);

/// <summary>Version is required to edit an existing profile and ignored when the profile is first created.</summary>
public sealed record UpsertBrokerProfileRequest(
    [param: Required, StringLength(200, MinimumLength = 2)] string OperatingArea,
    [param: Range(0, 60)] int YearsExperience,
    long? Version = null);

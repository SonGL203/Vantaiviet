using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record UpdateVehicleRequest(
    [param: Required, StringLength(20, MinimumLength = 3)] string LicensePlate,
    [param: Required, StringLength(50, MinimumLength = 2)] string VehicleType,
    [param: Range(typeof(decimal), "1", "1000000")] decimal MaxPayloadKg);

using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record CreateVehicleRequest(
    [param: Required, StringLength(20)] string LicensePlate,
    [param: Required, StringLength(50)] string VehicleType,
    [param: Range(1, 100000)] decimal MaxPayloadKg);

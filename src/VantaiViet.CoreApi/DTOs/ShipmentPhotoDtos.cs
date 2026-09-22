using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

/// <summary>Describes a stored photo; the image itself is fetched separately so lists stay small.</summary>
public sealed record ShipmentPhotoInfo(Guid Id, string ContentType, int SizeBytes, DateTimeOffset CreatedAt);

public sealed record UploadShipmentPhotoRequest([param: Required] string ContentType, [param: Required] byte[] Content);

public sealed record ShipmentPhotoContent(string ContentType, byte[] Content);

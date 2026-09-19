using System.ComponentModel.DataAnnotations;
namespace VantaiViet.CoreApi.DTOs;
public sealed record GpsPointRequest(Guid PointId,
    [param: Range(-90,90)] double Latitude,
    [param: Range(-180,180)] double Longitude,
    [param: Range(0,1000)] double AccuracyMeters,
    DateTimeOffset RecordedAt, bool Simulated = false);
public sealed record GpsBatchRequest([param: Required,MinLength(1),MaxLength(100)] GpsPointRequest[] Points);
public sealed record GpsPointResponse(long Id,Guid PointId,double Latitude,double Longitude,double AccuracyMeters,DateTimeOffset RecordedAt,DateTimeOffset ReceivedAt,bool Simulated);
public sealed record GpsBatchResponse(int Inserted,int Duplicates);
public sealed record GpsHistoryResponse(IReadOnlyList<GpsPointResponse> Points,long NextAfterId,bool HasMore);
public sealed record CoordinateDto([param: Range(-90,90)] double Latitude,[param: Range(-180,180)] double Longitude);
public sealed record SetTripStopsRequest([param: Required] CoordinateDto Pickup,[param: Required] CoordinateDto Delivery);
public sealed record TrackingSnapshot(Guid TripId,string Status,string Title,string PickupAddress,string DeliveryAddress,
    CoordinateDto? Pickup,CoordinateDto? Delivery,GpsPointResponse? Latest,bool Stale,bool CanSend,bool CanManage);
public sealed record TrackingTripResponse(Guid TripId,string Title,string Status);
public sealed record TrackingParticipantResponse(Guid UserId,string DisplayName);
public sealed record RouteResponse(string Status,double[][] Coordinates,double? DistanceMeters,double? DurationSeconds);

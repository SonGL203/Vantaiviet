using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IDriverReviewService
{
    Task<DriverProfileResponse> ReviewProfileAsync(Guid driverUserId, Guid reviewerUserId, ReviewSubmissionRequest request, CancellationToken cancellationToken);
    Task<VehicleResponse> ReviewVehicleAsync(Guid vehicleId, Guid reviewerUserId, ReviewSubmissionRequest request, CancellationToken cancellationToken);
}

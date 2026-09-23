using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IDriverReviewService
{
    /// <summary>Driver profile review queue, newest submission first. <paramref name="status"/> is optional.</summary>
    Task<IReadOnlyList<DriverProfileListItem>> ListDriverProfilesAsync(string? status, CancellationToken cancellationToken);

    /// <summary>Vehicle review queue, newest submission first. <paramref name="status"/> is optional.</summary>
    Task<IReadOnlyList<VehicleListItem>> ListVehiclesAsync(string? status, CancellationToken cancellationToken);

    Task<DriverProfileResponse> ReviewProfileAsync(Guid driverUserId, Guid reviewerUserId, ReviewSubmissionRequest request, CancellationToken cancellationToken);
    Task<VehicleResponse> ReviewVehicleAsync(Guid vehicleId, Guid reviewerUserId, ReviewSubmissionRequest request, CancellationToken cancellationToken);

    /// <summary>Driver-report queue ("Báo cáo tài xế"), newest first. <paramref name="status"/> is optional.</summary>
    Task<IReadOnlyList<DriverReportListItem>> ListDriverReportsAsync(string? status, CancellationToken cancellationToken);
    Task<DriverReportListItem> ReviewDriverReportAsync(Guid reportId, Guid reviewerUserId, DriverReportReviewRequest request, CancellationToken cancellationToken);
}

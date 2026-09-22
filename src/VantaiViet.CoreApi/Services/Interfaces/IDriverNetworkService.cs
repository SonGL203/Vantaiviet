using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IDriverNetworkService
{
    // Feature 16: groups.
    Task<ShipmentResult<DriverGroupResponse>> CreateGroupAsync(DriverGroupRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<DriverGroupResponse>>> ListGroupsAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<DriverGroupResponse>> RenameGroupAsync(Guid id, DriverGroupRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> DeleteGroupAsync(Guid id, CancellationToken cancellationToken);

    // Features 14-15: saved and favourite drivers.
    Task<ShipmentResult<SavedDriverResponse>> SaveAsync(SaveDriverRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<SavedDriverResponse>>> ListAsync(Guid? groupId, bool favoritesOnly, CancellationToken cancellationToken);
    Task<ShipmentResult<SavedDriverResponse>> UpdateAsync(Guid id, UpdateSavedDriverRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken);

    // Feature 17: invite code and who joined through it.
    Task<ShipmentResult<InviteCodeResponse>> InviteCodeAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<ReferredDriverResponse>>> ReferralsAsync(CancellationToken cancellationToken);

    // Feature 23: reporting a driver.
    Task<ShipmentResult<DriverReportResponse>> ReportDriverAsync(Guid driverUserId, DriverReportRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<DriverReportResponse>>> MyReportsAsync(CancellationToken cancellationToken);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize, Route("api/account/kyc"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountKycController(IAccountKycService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.GetAsync(cancellationToken), "kyc");
    }

    [HttpPut("identity")]
    public async Task<IActionResult> SaveIdentityAsync(SaveKycIdentityRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.SaveIdentityAsync(request, cancellationToken), "kyc");
    }

    [HttpPut("documents/{documentType}"), RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocumentAsync(string documentType, UploadKycDocumentRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.UploadDocumentAsync(documentType, request, cancellationToken), "kyc");
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.SubmitAsync(cancellationToken), "kyc");
    }
}

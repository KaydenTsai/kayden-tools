using Kayden.Commons.Common;
using Kayden.Commons.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers.Common;

/// <summary>
/// 健康檢查 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "common")]
[Tags("Health")]
[Produces("application/json")]
public class HealthController(IDateTimeService dateTimeService) : ControllerBase
{
    /// <summary>
    /// 取得服務健康狀態
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(ApiResponse.Ok(new
        {
            status = "healthy",
            timestamp = dateTimeService.UtcNow
        }));
    }
}

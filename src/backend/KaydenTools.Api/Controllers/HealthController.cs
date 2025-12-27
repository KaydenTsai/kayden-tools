using Kayden.Commons.Common;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers;

/// <summary>
/// 健康檢查 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
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
            timestamp = DateTime.UtcNow
        }));
    }
}

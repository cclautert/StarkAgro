using Microsoft.AspNetCore.Mvc;

namespace StarkAgroAPI.Controllers
{
    /// <summary>
    /// Health check para ALB/ECS (path /v1/ ou /v1/health).
    /// </summary>
    [Route("v1")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        [HttpGet("health")]
        public IActionResult Get() => Ok();
    }
}

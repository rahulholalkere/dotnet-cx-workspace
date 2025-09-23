#pragma warning disable IDE0130

using Microsoft.AspNetCore.Mvc;

namespace Liferay.Sample.Etc.DotNet.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ReadyController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { Message = "READY", Time = DateTime.UtcNow });
        }
    }
}

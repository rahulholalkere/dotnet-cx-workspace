#pragma warning disable IDE0130

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

using System.Security.Claims;

namespace Liferay.Sample.Etc.DotNet.Controllers
{
    [ApiController]
    [Route("/object/action/1")]
    public class ObjectActionController : BaseRestController<ObjectActionController>
    {
        public ObjectActionController(ILogger<ObjectActionController> logger) : base(logger)
        {

        }

        [HttpPost]
        public IActionResult Post([FromBody] JsonElement json)
        {
            var jwt = HttpContext.Request.Headers.Authorization.ToString();

            Logger.LogInformation("Received JSON:\n {json}", json);

            return Ok(json);
        }
    }
}

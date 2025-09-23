using Microsoft.AspNetCore.Mvc;

public abstract class BaseRestController<T> : ControllerBase
{
    protected readonly ILogger<T> Logger;

    protected BaseRestController(ILogger<T> logger)
    {
        Logger = logger;
    }

    // Optional: add shared logic here
    protected IActionResult RespondWithError(string message)
    {
        Logger.LogError(message);
        return BadRequest(new { error = message });
    }
}

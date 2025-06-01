using Microsoft.AspNetCore.Mvc;

namespace FeatBit.EvaluationServer.Broker.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet("ready")]
    public IActionResult Ready() => Ok();

    [HttpGet("live")]
    public IActionResult Live() => Ok();
} 
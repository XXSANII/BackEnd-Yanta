using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YantaBackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpGet("test")]
    [Authorize]
    public IActionResult TestAuthentication()
    {
        return Ok(new
        {
            authenticated = true,
            userId = User.FindFirst("sub")?.Value,
            message = "JWT validation successful"
        });
    }
}
using System.ComponentModel.DataAnnotations;
using InspectNow.Api.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InspectNow.Api.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn) : ControllerBase
{
    [HttpGet("csrf")]
    public object Csrf([FromServices] IAntiforgery antiforgery)
    {
        Response.Headers.CacheControl = "no-store";
        return new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken };
    }

    [HttpGet]
    [Authorize(Policy = "LocalAccount")]
    public async Task<IActionResult> Current([FromServices] PlatformOwner owner)
    {
        Response.Headers.CacheControl = "no-store";
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(new { user.Email, CanProvision = owner.IsOwner(User) });
    }

    [HttpPost("register")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Register(Credentials input)
    {
        if (User.Identity?.IsAuthenticated == true) return Conflict();
        var email = input.Email.Trim();
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["account"] = result.Errors.Select(error => error.Description).ToArray()
            }));
        await signIn.SignInAsync(user, isPersistent: false);
        return NoContent();
    }

    [HttpPost("login")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Login(Credentials input)
    {
        var result = await signIn.PasswordSignInAsync(input.Email.Trim(), input.Password,
            isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? NoContent() : Unauthorized(new { detail = "Sign-in failed. Check your credentials and try again." });
    }

    [HttpPost("logout")]
    [Authorize(Policy = "LocalAccount")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    public sealed record Credentials(
        [Required, EmailAddress, StringLength(254)] string Email,
        [Required, StringLength(128)] string Password);
}

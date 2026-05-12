using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ObfusCal.Api.Controllers;

[Route("account")]
public sealed class AccountController : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect(safeReturnUrl);

        return Challenge(CreateChallengeProperties(safeReturnUrl), OpenIdConnectDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet("switch")]
    public IActionResult Switch([FromQuery] string? returnUrl = null)
    {
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);
        return Challenge(CreateChallengeProperties(safeReturnUrl, prompt: "select_account"), OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("logout")]
    public IActionResult Logout([FromQuery] string? returnUrl = null)
    {
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);
        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = safeReturnUrl
            },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet("access-denied")]
    public IActionResult AccessDenied([FromQuery] string? returnUrl = null)
    {
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);
        return Content($"Access denied. Return URL: {safeReturnUrl}", "text/plain");
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        return Uri.TryCreate(returnUrl, UriKind.Relative, out _)
               && returnUrl.StartsWith("/", StringComparison.Ordinal)
            ? returnUrl
            : "/";
    }

    private static AuthenticationProperties CreateChallengeProperties(string redirectUri, string? prompt = null)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        if (!string.IsNullOrWhiteSpace(prompt))
            properties.Parameters["prompt"] = prompt;

        return properties;
    }
}


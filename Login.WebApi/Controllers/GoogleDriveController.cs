using Login.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Login.WebApi.Controllers;

// Conecta UNA sola cuenta de Google (la del despacho, Gmail personal) que
// va a alojar los documentos de TODOS los casos, sin importar qué usuario
// de la App los suba. No es "cada usuario con su cuenta" — ver GoogleDriveService.
[ApiController]
[Route("api/googledrive")]
public class GoogleDriveController : ControllerBase
{
    private readonly GoogleDriveService _drive;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;

    public GoogleDriveController(GoogleDriveService drive, IMemoryCache cache, IConfiguration config)
    {
        _drive = drive;
        _cache = cache;
        _config = config;
    }

    public record StatusResponse(bool Connected, string? Email, DateTime? ConnectedAt);
    public record StartResponse(string Url);

    [HttpGet("status")]
    [Authorize(Roles = "r-admin")]
    public async Task<ActionResult<StatusResponse>> Status()
    {
        var conn = await _drive.GetConnectionAsync();
        return Ok(new StatusResponse(conn is not null, conn?.ConnectedEmail, conn?.ConnectedAt));
    }

    // Devuelve la URL de Google para que el FRONTEND navegue ahí
    // (window.location.href = url), en vez de redirigir desde el propio
    // endpoint — así el llamado sí puede llevar el Bearer token normal.
    [HttpGet("oauth/start")]
    [Authorize(Roles = "r-admin")]
    public ActionResult<StartResponse> Start()
    {
        var state = Guid.NewGuid().ToString("N");
        _cache.Set($"gdrive-oauth-state:{state}", true, TimeSpan.FromMinutes(10));

        var redirectUri = BuildRedirectUri();
        var url = _drive.BuildAuthorizationUrl(redirectUri, state);
        return Ok(new StartResponse(url));
    }

    // A este endpoint redirige Google directamente (navegación de browser,
    // sin nuestro Bearer token) — por eso es anónimo, y por eso validamos
    // "state" contra lo que guardamos en Start() como mitigación CSRF.
    [HttpGet("oauth/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        var frontendUrl = (_config["GoogleDrive:FrontendRedirectUrl"] ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(frontendUrl))
            return Problem("Falta configurar GoogleDrive:FrontendRedirectUrl.", statusCode: StatusCodes.Status500InternalServerError);

        if (!string.IsNullOrWhiteSpace(error))
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString(error)}");

        var stateKey = $"gdrive-oauth-state:{state}";
        if (string.IsNullOrWhiteSpace(state) || !_cache.TryGetValue(stateKey, out _))
            return Redirect($"{frontendUrl}?error=invalid_state");
        _cache.Remove(stateKey);

        if (string.IsNullOrWhiteSpace(code))
            return Redirect($"{frontendUrl}?error=missing_code");

        try
        {
            var redirectUri = BuildRedirectUri();
            var email = await _drive.ExchangeCodeAndSaveAsync(code, redirectUri);
            return Redirect($"{frontendUrl}?connected=1&email={Uri.EscapeDataString(email)}");
        }
        catch (Exception ex)
        {
            return Redirect($"{frontendUrl}?error={Uri.EscapeDataString(ex.Message)}");
        }
    }

    [HttpPost("disconnect")]
    [Authorize(Roles = "r-admin")]
    public async Task<IActionResult> Disconnect()
    {
        await _drive.DisconnectAsync();
        return NoContent();
    }

    // Debe coincidir EXACTO con un "Authorized redirect URI" del cliente
    // OAuth en Google Cloud Console.
    private string BuildRedirectUri() => $"{Request.Scheme}://{Request.Host}/api/googledrive/oauth/callback";
}

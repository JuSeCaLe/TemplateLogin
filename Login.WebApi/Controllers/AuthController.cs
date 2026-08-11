using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Login.Infrastructure.Data.Identity;
using Login.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Login.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly DemandanteAuthService _demandanteAuthService;
        private readonly IConfiguration _config;

        public AuthController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            DemandanteAuthService demandanteAuthService,
            IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _demandanteAuthService = demandanteAuthService;
            _config = config;
        }

        public record LoginRequest(string Email, string Password);
        public record DemandanteInfo(string Id, string Name);
        public record LoginResponse(string AccessToken, int ExpiresIn, object User, string[] Roles, string[] DemandanteIds, DemandanteInfo[] Demandantes);

        // Para cada rol-demandante activo que tiene asignado el usuario, devuelve
        // su Id/Name. Un usuario "de demandante" solo verá casos de estos.
        // Delega en DemandanteAuthService (compartido con CasesController, que lo
        // usa para autorizar en vivo en cada request, no solo al loguear).
        private async Task<DemandanteInfo[]> ResolveDemandantesAsync(AppUser user) =>
            (await _demandanteAuthService.ResolveActiveDemandanteRolesAsync(user))
                .Select(r => new DemandanteInfo(r.Id, r.Name!))
                .ToArray();

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
        {
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user is null) return Unauthorized(new { message = "Credenciales inválidas" });
            if (!user.Active) return Unauthorized(new { message = "Usuario inactivo" });

            var passwordOk = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
            if (!passwordOk.Succeeded) return Unauthorized(new { message = "Credenciales inválidas" });

            var roles = (await _userManager.GetRolesAsync(user)).ToArray();
            var demandantes = await ResolveDemandantesAsync(user);

            var jwtSection = _config.GetSection("Jwt");
            var issuer = jwtSection["Issuer"]!;
            var audience = jwtSection["Audience"]!;
            var key = jwtSection["Key"]!;
            var expiresMinutes = int.TryParse(jwtSection["ExpiresMinutes"], out var m) ? m : 60;

            var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? req.Email),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? req.Email),
        };

            // roles -> ClaimTypes.Role (esto habilita [Authorize(Roles="...")])
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            // demandanteId -> restringe qué Cases puede ver/editar este usuario (ver CasesController.AuthorizedQuery)
            claims.AddRange(demandantes.Select(d => new Claim("demandanteId", d.Id)));

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.AddMinutes(expiresMinutes);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new LoginResponse(
                AccessToken: accessToken,
                ExpiresIn: (int)TimeSpan.FromMinutes(expiresMinutes).TotalSeconds,
                User: new { id = user.Id, email = user.Email, userName = user.UserName },
                Roles: roles,
                DemandanteIds: demandantes.Select(d => d.Id).ToArray(),
                Demandantes: demandantes
            ));
        }

        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return Unauthorized();

            var roles = (await _userManager.GetRolesAsync(user)).ToArray();
            var demandantes = await ResolveDemandantesAsync(user);

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                userName = user.UserName,
                roles,
                demandanteIds = demandantes.Select(d => d.Id).ToArray(),
                demandantes
            });
        }

        [Authorize(Roles = "r-admin")]
        [HttpGet("admin-only")]
        public IActionResult AdminOnly() => Ok("ok");

        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping() => Ok(new { ok = true, at = DateTime.UtcNow });

        [HttpGet("whoami")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult WhoAmI()
        {
            return Ok(new
            {
                isAuthenticated = User.Identity?.IsAuthenticated,
                name = User.Identity?.Name,
                claims = User.Claims.Select(c => new { c.Type, c.Value })
            });
        }

    }
}

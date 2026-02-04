using Login.Infrastructure.Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Login.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "r-admin")]
public class UsersController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;

    public UsersController(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public record UserDto(
        string Id,
        string Email,
        string FirstName,
        string LastName,
        string UserName,
        bool Active,
        DateTime CreatedAt,
        string[] Roles
    );

    public record CreateUserRequest(
        string Email,
        string FirstName,
        string LastName,
        string UserName,
        string Password,
        bool Active,
        string[]? Roles
    );

    public record UpdateUserRequest(
        string Email,
        string FirstName,
        string LastName,
        string UserName,
        bool Active
    );

    public record SetUserRolesRequest(string[] Roles);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        // Nota: incluir roles requiere una consulta por usuario (N+1).
        // Para volúmenes grandes, se optimiza luego.
        var users = _userManager.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToList();

        var result = new List<UserDto>(users.Count);

        foreach (var u in users)
        {
            var roles = (await _userManager.GetRolesAsync(u)).ToArray();
            result.Add(new UserDto(
                u.Id,
                u.Email ?? "",
                u.FirstName ?? "",
                u.LastName ?? "",
                u.UserName ?? "",
                u.Active,
                u.CreatedAt,
                roles
            ));
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        var u = await _userManager.FindByIdAsync(id);
        if (u is null) return NotFound(new { message = "Usuario no encontrado" });

        var roles = (await _userManager.GetRolesAsync(u)).ToArray();

        return Ok(new UserDto(
            u.Id,
            u.Email ?? "",
            u.FirstName ?? "",
            u.LastName ?? "",
            u.UserName ?? "",
            u.Active,
            u.CreatedAt,
            roles
        ));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email es requerido" });

        if (string.IsNullOrWhiteSpace(req.FirstName))
            return BadRequest(new { message = "FirstName es requerido" });

        if (string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new { message = "LastName es requerido" });

        if (string.IsNullOrWhiteSpace(req.UserName))
            return BadRequest(new { message = "UserName es requerido" });

        if (string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Password es requerido" });

        var email = req.Email.Trim();
        var userName = req.UserName.Trim();

        // Evitar duplicados (email)
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            return Conflict(new { message = "Ya existe un usuario con ese email" });

        var user = new AppUser
        {
            Email = email,
            FirstName = req.FirstName,
            LastName = req.LastName,
            UserName = userName,
            EmailConfirmed = true,
            Active = req.Active,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, req.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                message = string.Join("; ", createResult.Errors.Select(e => e.Description))
            });
        }

        // Roles opcionales
        if (req.Roles is { Length: > 0 })
        {
            var validRoles = new List<string>();
            foreach (var r in req.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (await _roleManager.RoleExistsAsync(r))
                    validRoles.Add(r);
                else
                    return BadRequest(new { message = $"Rol inválido: {r}" });
            }

            var roleResult = await _userManager.AddToRolesAsync(user, validRoles);
            if (!roleResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = string.Join("; ", roleResult.Errors.Select(e => e.Description))
                });
            }
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToArray();

        return Ok(new UserDto(
            user.Id,
            user.Email ?? "",
            user.FirstName ?? "",
            user.LastName ?? "",
            user.UserName ?? "",
            user.Active,
            user.CreatedAt,
            roles
        ));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound(new { message = "Usuario no encontrado" });

        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email es requerido" });

        if (string.IsNullOrWhiteSpace(req.FirstName))
            return BadRequest(new { message = "FirstName es requerido" });

        if (string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new { message = "LastName es requerido" });

        if (string.IsNullOrWhiteSpace(req.UserName))
            return BadRequest(new { message = "UserName es requerido" });

        var email = req.Email.Trim();
        var userName = req.UserName.Trim();

        // Si cambia email, validar duplicado
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing is not null && existing.Id != user.Id)
                return Conflict(new { message = "Ya existe un usuario con ese email" });
        }

        user.Email = email;
        user.NormalizedEmail = email.ToUpperInvariant();
        user.FirstName = req.FirstName;
        user.LastName = req.LastName;
        user.UserName = userName;
        user.NormalizedUserName = userName.ToUpperInvariant();
        user.Active = req.Active;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = string.Join("; ", result.Errors.Select(e => e.Description))
            });
        }

        return NoContent();
    }

    [HttpPut("{id}/roles")]
    public async Task<IActionResult> SetRoles(string id, [FromBody] SetUserRolesRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound(new { message = "Usuario no encontrado" });

        var desired = (req.Roles ?? Array.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // validar roles existentes
        foreach (var r in desired)
        {
            if (!await _roleManager.RoleExistsAsync(r))
                return BadRequest(new { message = $"Rol inválido: {r}" });
        }

        var current = (await _userManager.GetRolesAsync(user)).ToArray();

        var toRemove = current.Except(desired, StringComparer.OrdinalIgnoreCase).ToArray();
        var toAdd = desired.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();

        if (toRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = string.Join("; ", removeResult.Errors.Select(e => e.Description))
                });
            }
        }

        if (toAdd.Length > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = string.Join("; ", addResult.Errors.Select(e => e.Description))
                });
            }
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound(new { message = "Usuario no encontrado" });

        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == id)
            return BadRequest(new { message = "No puedes eliminar tu propio usuario" });

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = string.Join("; ", result.Errors.Select(e => e.Description))
            });
        }

        return NoContent();
    }


    [HttpPatch("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound(new { message = "Usuario no encontrado" });

        user.Active = !user.Active;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = string.Join("; ", result.Errors.Select(e => e.Description))
            });
        }

        return NoContent();
    }
}

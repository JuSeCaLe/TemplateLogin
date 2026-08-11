using System;
using Login.Infrastructure.Data.Identity;
using Login.Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Login.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "r-admin")]
    public class RolesController : ControllerBase
    {
        private static readonly string[] SystemRoleNames = ["r-admin", "r-user"];

        private readonly RoleManager<AppRole> _roleManager;
        private readonly DataContext _db;

        public RolesController(RoleManager<AppRole> roleManager, DataContext db)
        {
            _roleManager = roleManager;
            _db = db;
        }

        public record RoleDto(string Id, string Name, string? Description, bool Active, DateTime CreatedAt, bool IsDemandante);
        public record CreateRoleRequest(string Name, string? Description, bool Active, bool IsDemandante);
        public record UpdateRoleRequest(string Name, string? Description, bool Active, bool IsDemandante);

        private static RoleDto ToDto(AppRole r) => new(
            r.Id, r.Name!, r.Description, r.Active, r.CreatedAt, r.IsDemandante
        );

        private static bool IsSystemRole(string? name) =>
            name is not null && SystemRoleNames.Contains(name, StringComparer.OrdinalIgnoreCase);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll()
        {
            var roles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();

            return Ok(roles.Select(ToDto));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RoleDto>> GetById(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null)
                return NotFound(new { message = "Rol no encontrado" });

            return Ok(ToDto(role));
        }

        [HttpPost]
        public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest req)
        {
            var name = (req.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { message = "El nombre del rol es requerido" });

            if (await _roleManager.RoleExistsAsync(name))
                return Conflict(new { message = "El rol ya existe" });

            var role = new AppRole
            {
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
                Description = req.Description,
                Active = req.Active,
                IsDemandante = req.IsDemandante,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

            var created = await _roleManager.FindByNameAsync(name);
            return Ok(ToDto(created!));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateRoleRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "El nombre del rol es requerido" });

            var role = await _roleManager.FindByIdAsync(id);
            if (role is null)
                return NotFound(new { message = "Rol no encontrado" });

            if (IsSystemRole(role.Name))
                return Conflict(new { message = "Este es un rol del sistema y no puede modificarse" });

            var newName = req.Name.Trim();

            if (IsSystemRole(newName))
                return BadRequest(new { message = $"\"{newName}\" es un nombre reservado para roles del sistema" });

            // Evitar duplicados
            if (!string.Equals(role.Name, newName, StringComparison.OrdinalIgnoreCase) &&
            await _roleManager.RoleExistsAsync(newName))
            {
                return Conflict(new { message = "Ya existe un rol con ese nombre" });
            }

            role.Name = newName;
            role.NormalizedName = newName.ToUpperInvariant();
            role.Description = req.Description;
            role.Active = req.Active;
            role.IsDemandante = req.IsDemandante;

            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = string.Join("; ", result.Errors.Select(e => e.Description))
                });
            }

            return NoContent();
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null) return NotFound();

            if (IsSystemRole(role.Name))
                return Conflict(new { message = "Este es un rol del sistema y no puede eliminarse" });

            if (role.IsDemandante && await _db.Cases.AnyAsync(c => c.DemandanteRoleId == id))
                return Conflict(new { message = "No se puede eliminar: existen casos asociados a este rol" });

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

            return NoContent();
        }
    }
}

using System;
using Login.Infrastructure.Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Login.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "r-admin")]
    public class RolesController : ControllerBase
    {
        private readonly RoleManager<AppRole> _roleManager;
        
        public RolesController(RoleManager<AppRole> roleManager)
        {
            _roleManager = roleManager;
        }

        public record  RoleDto(string Id, string Name, string? Description, bool Active, DateTime CreatedAt);
        public record CreateRoleRequest(string Name, string? Description, bool Active);
        public record UpdateRoleRequest(string Name, string? Description, bool Active);


        [HttpGet]
        public ActionResult<IEnumerable<RoleDto>> GetAll()
        {
            var roles = _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => new RoleDto(r.Id, r.Name!, r.Description, r.Active, r.CreatedAt))
                .ToList();
            return Ok(roles);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RoleDto>> GetById(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null)
                return NotFound(new { message = "Rol no encontrado" });

            return Ok(new RoleDto(
                role.Id,
                role.Name!,
                role.Description,
                role.Active,
                role.CreatedAt
            ));
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
                CreatedAt = DateTime.UtcNow
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

            var created = await _roleManager.FindByNameAsync(name);
            return Ok(new RoleDto(created!.Id, created.Name!, created.Description, created.Active, created.CreatedAt));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateRoleRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "El nombre del rol es requerido" });

            var role = await _roleManager.FindByIdAsync(id);
            if (role is null)
                return NotFound(new { message = "Rol no encontrado" });

            var newName = req.Name.Trim();

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

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

            return NoContent();
        }
    }
}

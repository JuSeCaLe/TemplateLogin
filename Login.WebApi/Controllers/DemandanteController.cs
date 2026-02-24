using Login.Infrastructure.Model;
using Login.Infrastructure.Model.Common;
using Login.Infrastructure.Model.Parametros;
using Login.WebApi.Controllers.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Login.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "r-admin")]
public class DemandanteController : ControllerBase
{
    private readonly DataContext _db;
    public DemandanteController(DataContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParametricDto>>> GetAll()
        => Ok(await _db.Demandante
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ParametricDto(x.Id, x.Name, x.Description, x.Active, x.CreatedAt))
            .ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParametricDto>> GetById(Guid id)
    {
        var x = await _db.Demandante.FindAsync(id);
        if (x is null) return NotFound();
        return Ok(new ParametricDto(x.Id, x.Name, x.Description, x.Active, x.CreatedAt));
    }

    [HttpPost]
    public async Task<ActionResult<ParametricDto>> Create(CreateParametricRequest req)
    {
        var name = req.Name.Trim();
        if (await _db.Demandante.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "Ya existe un registro con ese nombre" });

        var entity = new Demandante
        {
            Name = name,
            Description = req.Description?.Trim(),
            Active = req.Active,
            CreatedAt = DateTime.UtcNow
        };

        _db.Demandante.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new ParametricDto(entity.Id, entity.Name, entity.Description, entity.Active, entity.CreatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateParametricRequest req)
    {
        var entity = await _db.Demandante.FindAsync(id);
        if (entity is null) return NotFound();

        var name = req.Name.Trim();
        if (await _db.Demandante.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "Ya existe un registro con ese nombre" });

        entity.Name = name;
        entity.Description = req.Description?.Trim();
        entity.Active = req.Active;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var entity = await _db.Demandante.FindAsync(id);
        if (entity is null) return NotFound();

        entity.Active = !entity.Active;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Demandante.FindAsync(id);
        if (entity is null) return NotFound();

        _db.Demandante.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

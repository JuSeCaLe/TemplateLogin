using Login.Infrastructure.Model;
using Login.Infrastructure.Model.Parametros;
using Login.WebApi.Controllers.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Login.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "r-admin")]
public class JuzgadosController : ControllerBase
{
    private readonly DataContext _db;
    public JuzgadosController(DataContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CourtDto>>> GetAll()
        => Ok(await _db.Juzgado
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CourtDto(x.Id, x.Name, x.Description, x.Active, x.CreatedAt, x.City))
            .ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CourtDto>> GetById(Guid id)
    {
        var x = await _db.Juzgado.FindAsync(id);
        if (x is null) return NotFound();
        return Ok(new CourtDto(x.Id, x.Name, x.Description, x.Active, x.CreatedAt, x.City));
    }

    [HttpPost]
    public async Task<ActionResult<CourtDto>> Create(CreateCourtRequest req)
    {
        var name = req.Name.Trim();
        if (await _db.Juzgado.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "Ya existe un registro con ese nombre" });

        var entity = new Juzgado
        {
            Name = name,
            Description = req.Description?.Trim(),
            City = req.City.Trim(),
            Active = req.Active,
            CreatedAt = DateTime.UtcNow
        };

        _db.Juzgado.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new CourtDto(entity.Id, entity.Name, entity.Description, entity.Active, entity.CreatedAt, entity.City));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCourtRequest req)
    {
        var entity = await _db.Juzgado.FindAsync(id);
        if (entity is null) return NotFound();

        var name = req.Name.Trim();
        if (await _db.Juzgado.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "Ya existe un registro con ese nombre" });

        entity.Name = name;
        entity.Description = req.Description?.Trim();
        entity.City = req.City.Trim();
        entity.Active = req.Active;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var entity = await _db.Juzgado.FindAsync(id);
        if (entity is null) return NotFound();

        entity.Active = !entity.Active;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Juzgado.FindAsync(id);
        if (entity is null) return NotFound();

        _db.Juzgado.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

using Login.Infrastructure.Model;
using Login.Infrastructure.Services;
using Login.WebApi.Controllers.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Login.WebApi.Controllers;

// Módulo admin para subir/reemplazar/eliminar las plantillas de memoriales.
// Separado de MemorialsController (que usan TODOS los usuarios para generar
// memoriales) porque acá el CRUD completo es solo para r-admin.
[ApiController]
[Route("api/memorial-templates")]
[Authorize(Roles = "r-admin")]
public class MemorialTemplatesController : ControllerBase
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const long MaxFileSize = 20_000_000; // 20 MB, generoso para un .docx

    private readonly DataContext _db;

    public MemorialTemplatesController(DataContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MemorialTemplateAdminDto>>> GetAll()
    {
        var templates = await _db.MemorialTemplates
            .OrderBy(t => t.Name)
            .ToListAsync();

        return Ok(templates.Select(ToDto));
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<ActionResult<MemorialTemplateAdminDto>> Create([FromForm] string name, IFormFile file)
    {
        var validation = ValidateFile(file);
        if (validation is not null) return validation;

        var trimmedName = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            return BadRequest(new { message = "El nombre de la plantilla es requerido." });

        if (await _db.MemorialTemplates.AnyAsync(t => t.Name.ToLower() == trimmedName.ToLower()))
            return Conflict(new { message = "Ya existe una plantilla con ese nombre." });

        var content = await ReadAllBytesAsync(file);
        var now = DateTime.UtcNow;

        var entity = new MemorialTemplate
        {
            Name = trimmedName,
            FileName = file.FileName,
            Content = content,
            DetectedPlaceholders = string.Join(", ", MemorialGenerationService.ExtractPlaceholders(content)),
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.MemorialTemplates.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(ToDto(entity));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateMemorialTemplateRequest req)
    {
        var entity = await _db.MemorialTemplates.FindAsync(id);
        if (entity is null) return NotFound();

        var trimmedName = (req.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            return BadRequest(new { message = "El nombre de la plantilla es requerido." });

        if (await _db.MemorialTemplates.AnyAsync(t => t.Id != id && t.Name.ToLower() == trimmedName.ToLower()))
            return Conflict(new { message = "Ya existe una plantilla con ese nombre." });

        entity.Name = trimmedName;
        entity.Active = req.Active;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    // "Editar" la plantilla, en el sentido que pidió el usuario, es
    // reemplazar el archivo — el Id/Name no cambian, así que los memoriales
    // que ya se generaron con la versión vieja no se ven afectados (no se
    // guardan, se descargan al momento) y cualquier selección existente de
    // esta plantilla sigue apuntando al mismo registro con el contenido nuevo.
    [HttpPost("{id:int}/replace")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<ActionResult<MemorialTemplateAdminDto>> ReplaceFile(int id, IFormFile file)
    {
        var validation = ValidateFile(file);
        if (validation is not null) return validation;

        var entity = await _db.MemorialTemplates.FindAsync(id);
        if (entity is null) return NotFound();

        var content = await ReadAllBytesAsync(file);

        entity.FileName = file.FileName;
        entity.Content = content;
        entity.DetectedPlaceholders = string.Join(", ", MemorialGenerationService.ExtractPlaceholders(content));
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var entity = await _db.MemorialTemplates.FindAsync(id);
        if (entity is null || entity.Content is null || entity.Content.Length == 0) return NotFound();

        return File(entity.Content, DocxContentType, entity.FileName);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.MemorialTemplates.FindAsync(id);
        if (entity is null) return NotFound();

        // Sin problema de integridad referencial: los memoriales se generan
        // al vuelo y se descargan, ningún caso queda "enganchado" a una
        // plantilla en particular.
        _db.MemorialTemplates.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private ActionResult? ValidateFile(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Archivo requerido." });

        var isDocx = Path.GetExtension(file.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase);
        if (!isDocx)
            return BadRequest(new { message = "El archivo debe ser un .docx de Word." });

        return null;
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static MemorialTemplateAdminDto ToDto(MemorialTemplate t) => new(
        t.Id,
        t.Name,
        t.FileName,
        t.Active,
        t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        t.UpdatedAt.ToString("yyyy-MM-dd HH:mm"),
        t.DetectedPlaceholders.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList()
    );
}

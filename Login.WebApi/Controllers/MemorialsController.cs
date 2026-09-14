using System.IO.Compression;
using System.Linq.Expressions;
using System.Security.Claims;
using Login.Infrastructure.Data.Identity;
using Login.Infrastructure.Model;
using Login.Infrastructure.Model.Cases;
using Login.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Login.WebApi.Controllers;

[ApiController]
[Route("api/memorials")]
[Authorize]
public class MemorialsController : ControllerBase
{
    private readonly DataContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly DemandanteAuthService _demandanteAuthService;
    private readonly MemorialGenerationService _generator;

    public MemorialsController(DataContext db, UserManager<AppUser> userManager, DemandanteAuthService demandanteAuthService, MemorialGenerationService generator)
    {
        _db = db;
        _userManager = userManager;
        _demandanteAuthService = demandanteAuthService;
        _generator = generator;
    }

    public record MemorialTemplateDto(int Id, string Name);
    public record GenerateRequest(int TemplateId, int[] CaseIds);

    [HttpGet("templates")]
    public async Task<ActionResult<IEnumerable<MemorialTemplateDto>>> GetTemplates()
    {
        var templates = await _db.MemorialTemplates
            .Where(t => t.Active)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return Ok(templates.Select(t => new MemorialTemplateDto(t.Id, t.Name)));
    }

    // Genera el memorial para uno o varios casos a la vez. Un solo caso
    // devuelve el .docx directo; varios casos devuelven un .zip con uno por
    // caso. Mismo criterio de autorización que CasesController: admin ve
    // todo, un usuario con rol(es)-demandante solo sus propios casos.
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(GenerateRequest req)
    {
        if (req.CaseIds is null || req.CaseIds.Length == 0)
            return BadRequest(new { message = "Selecciona al menos un caso." });

        var template = await _db.MemorialTemplates.FirstOrDefaultAsync(t => t.Id == req.TemplateId && t.Active);
        if (template is null) return NotFound(new { message = "Plantilla no encontrada." });

        var auth = await GetAuthContextAsync();
        var cases = await AuthorizedQuery(auth)
            .Where(c => req.CaseIds.Contains(c.Id))
            .ToListAsync();

        if (cases.Count == 0)
            return NotFound(new { message = "No se encontraron casos autorizados con esos ids." });

        var generated = new List<(string FileName, byte[] Content)>();
        foreach (var c in cases)
        {
            byte[] bytes;
            try
            {
                bytes = _generator.Generate(template, BuildFieldValues(c));
            }
            catch (FileNotFoundException ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }

            var (defendantName, _) = ParseParty(c.Parties.FirstOrDefault(p => p.ProcessRole == "DEMANDADO")?.Person);
            var label = string.IsNullOrWhiteSpace(c.Process.Radicado) ? $"Caso-{c.Id}" : c.Process.Radicado;
            generated.Add((SafeFileName($"{template.Name} - {label} - {defendantName}.docx"), bytes));
        }

        if (generated.Count == 1)
        {
            return File(
                generated[0].Content,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                generated[0].FileName);
        }

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fileName, content) in generated)
            {
                var uniqueName = fileName;
                var i = 2;
                while (!usedNames.Add(uniqueName))
                    uniqueName = SafeFileName($"{Path.GetFileNameWithoutExtension(fileName)} ({i++}).docx");

                var entry = archive.CreateEntry(uniqueName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }

        return File(zipStream.ToArray(), "application/zip", SafeFileName($"{template.Name} - memoriales.zip"));
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static MemorialFieldValues BuildFieldValues(Case c)
    {
        var demandanteParty = c.Parties.FirstOrDefault(p => p.ProcessRole == "DEMANDANTE");
        var (demandanteName, _) = ParseParty(demandanteParty?.Person);
        var demandante = string.IsNullOrWhiteSpace(demandanteName) ? (c.DemandanteRole?.Name ?? "") : demandanteName;

        var demandadoParty = c.Parties.FirstOrDefault(p => p.ProcessRole == "DEMANDADO");
        var (demandadoName, demandadoCedula) = ParseParty(demandadoParty?.Person);

        return new MemorialFieldValues(
            Juzgado: c.Process.Court ?? "",
            Ciudad: c.Process.City ?? "",
            TipoProceso: c.Process.ProcessType ?? "",
            Demandante: demandante,
            Demandado: demandadoName,
            DemandadoCedula: demandadoCedula,
            Radicado: c.Process.Radicado ?? ""
        );
    }

    // Mismo formato "Nombre|Tipo|Documento" que usa CasesController.ParseDefendant.
    private static (string Name, string Document) ParseParty(string? person)
    {
        if (string.IsNullOrWhiteSpace(person)) return ("", "");

        var parts = person.Split('|').Select(p => p.Trim()).ToArray();
        return parts.Length switch
        {
            >= 3 => (parts[0], parts[2]),
            2 => (parts[0], parts[1]),
            _ => (parts[0], "")
        };
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '-');
        return name.Length > 150 ? name[..150] + Path.GetExtension(name) : name;
    }

    private IQueryable<Case> QueryWithIncludes() =>
        _db.Cases
            .Include(c => c.DemandanteRole)
            .Include(c => c.Parties);

    public readonly record struct AuthContext(bool IsAdmin, bool HasAnyDemandanteRole, string[] DemandanteIds);

    // Duplica el criterio de CasesController.GetAuthContextAsync — ver ahí
    // el porqué (resolución en vivo contra la BD, no contra el JWT).
    private async Task<AuthContext> GetAuthContextAsync()
    {
        if (User.IsInRole("r-admin"))
            return new AuthContext(true, false, Array.Empty<string>());

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return new AuthContext(false, true, Array.Empty<string>());

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return new AuthContext(false, true, Array.Empty<string>());

        var assigned = await _demandanteAuthService.ResolveAssignedDemandanteRolesAsync(user);
        var activeIds = assigned.Where(d => d.Active).Select(d => d.Id).ToArray();

        return new AuthContext(false, assigned.Count > 0, activeIds);
    }

    private IQueryable<Case> AuthorizedQuery(AuthContext auth)
    {
        var query = QueryWithIncludes();

        if (auth.IsAdmin || !auth.HasAnyDemandanteRole)
            return query;

        if (auth.DemandanteIds.Length == 0)
            return query.Where(c => false);

        return query.Where(BuildDemandanteFilter(auth.DemandanteIds));
    }

    private static Expression<Func<Case, bool>> BuildDemandanteFilter(string[] demandanteIds)
    {
        var param = Expression.Parameter(typeof(Case), "c");
        var prop = Expression.Property(param, nameof(Case.DemandanteRoleId));

        Expression? body = null;
        foreach (var id in demandanteIds)
        {
            var eq = Expression.Equal(prop, Expression.Constant(id));
            body = body is null ? eq : Expression.OrElse(body, eq);
        }

        return Expression.Lambda<Func<Case, bool>>(body!, param);
    }
}

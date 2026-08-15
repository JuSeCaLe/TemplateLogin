using System.Linq.Expressions;
using System.Security.Claims;
using ClosedXML.Excel;
using Login.Infrastructure.Data.Identity;
using Login.Infrastructure.Model;
using Login.Infrastructure.Model.Cases;
using Login.Infrastructure.Services;
using Login.WebApi.Controllers.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Login.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly DataContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly DemandanteAuthService _demandanteAuthService;
    private readonly GoogleDriveService _drive;

    public CasesController(DataContext db, UserManager<AppUser> userManager, DemandanteAuthService demandanteAuthService, GoogleDriveService drive)
    {
        _db = db;
        _userManager = userManager;
        _demandanteAuthService = demandanteAuthService;
        _drive = drive;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CaseDto>>> GetAll()
    {
        var auth = await GetAuthContextAsync();
        var cases = await AuthorizedQuery(auth).OrderByDescending(c => c.CreatedAt).ToListAsync();
        return Ok(cases.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CaseDto>> GetById(int id)
    {
        var c = await FindCase(id);
        return c is null ? NotFound() : Ok(ToDto(c));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<CaseDto>>> Search([FromQuery] string radicado)
    {
        var term = radicado.Trim();
        var auth = await GetAuthContextAsync();
        var cases = await AuthorizedQuery(auth)
            .Where(c => c.Process.Radicado.Contains(term))
            .ToListAsync();
        return Ok(cases.Select(ToDto));
    }

    // Mismo criterio de autorización que GetAll: admin exporta todo, un
    // usuario con rol(es)-demandante solo exporta sus propios casos.
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var auth = await GetAuthContextAsync();
        var cases = await AuthorizedQuery(auth)
            .OrderBy(c => c.Process.Radicado)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Casos");

        string[] headers =
        [
            "Nombre demandado", "Cédula demandado", "Obligaciones", "Capital",
            "Fecha presentación demanda", "Juzgado", "Radicación", "Tipo de proceso",
            "Etapa actual", "Observaciones"
        ];
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var c in cases)
        {
            var defendant = c.Parties.FirstOrDefault(p => p.ProcessRole == "DEMANDADO");
            var (name, document) = ParseDefendant(defendant?.Person);

            var lastStage = c.ProcessStages.OrderByDescending(s => s.Id).FirstOrDefault();
            var currentStage = lastStage is null
                ? ""
                : string.Join(" / ", new[] { lastStage.StageName, lastStage.SubStageName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            // "Observaciones" consolida el historial de observaciones de cada
            // etapa del proceso (incluye las de tipo "Impulso procesal") en una
            // sola celda, en orden cronológico.
            var observations = string.Join(" | ", c.ProcessStages
                .Where(s => !string.IsNullOrWhiteSpace(s.Observation))
                .OrderBy(s => s.Id)
                .Select(s => $"[{s.CreatedAt}] {s.Observation}"));

            sheet.Cell(row, 1).Value = name;
            sheet.Cell(row, 2).Value = document;
            sheet.Cell(row, 3).Value = c.FinancialInfo?.Obligations ?? "";
            sheet.Cell(row, 4).Value = c.FinancialInfo?.Capital ?? 0;
            sheet.Cell(row, 5).Value = c.Process.FiledAt ?? "";
            sheet.Cell(row, 6).Value = c.Process.Court;
            sheet.Cell(row, 7).Value = c.Process.Radicado;
            sheet.Cell(row, 8).Value = c.Process.ProcessType;
            sheet.Cell(row, 9).Value = currentStage;
            sheet.Cell(row, 10).Value = observations;
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var fileName = $"casos_{DateTime.UtcNow:yyyyMMdd}.xlsx";
        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // El campo Person de CaseParty se guarda como "Nombre|Tipo|Documento".
    // Se tolera el formato legado "Nombre | Documento" (sin tipo) para no
    // romper casos creados antes de estandarizarlo.
    private static (string Name, string Document) ParseDefendant(string? person)
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

    [HttpPost]
    public async Task<ActionResult<CaseDto>> Create(CreateCaseRequest req)
    {
        var auth = await GetAuthContextAsync();
        if (!IsDemandanteAllowed(auth, req.DemandanteRoleId))
            return Forbid();

        var entity = BuildEntity(req);
        _db.Cases.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ToDto(await FindCase(entity.Id) ?? entity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CaseDto>> Update(int id, UpdateCaseRequest req)
    {
        var auth = await GetAuthContextAsync();
        if (!IsDemandanteAllowed(auth, req.DemandanteRoleId))
            return Forbid();

        var entity = await FindCase(id);
        if (entity is null) return NotFound();

        ApplyUpdate(entity, req);
        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    [HttpPost("{id:int}/stages")]
    public async Task<ActionResult<CaseDto>> AddStage(int id, AddProcessStageRequest req)
    {
        var entity = await FindCase(id);
        if (entity is null) return NotFound();

        entity.ProcessStages.Add(new CaseProcessStage
        {
            CaseId = id,
            CreatedAt = req.StageDate ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            StageName = req.StageName.Trim(),
            SubStageName = req.SubStageName?.Trim() ?? "",
            Observation = req.Observation?.Trim()
        });
        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    [HttpPost("{id:int}/notes")]
    public async Task<ActionResult<CaseDto>> AddNote(int id, AddProceduralNoteRequest req)
    {
        var entity = await FindCase(id);
        if (entity is null) return NotFound();

        entity.ProceduralNotes.Add(new CaseProceduralNote
        {
            CaseId = id,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Text = req.Text.Trim()
        });
        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    // ── Documentos (Google Drive) ────────────────────────────────────────

    // Crea (si no existe) la subcarpeta de Drive del caso y devuelve su id/url.
    // Idempotente: si ya tiene carpeta, solo la devuelve.
    [HttpPost("{id:int}/drive-folder")]
    public async Task<ActionResult<DriveFolderDto>> CreateDriveFolder(int id)
    {
        if (!_drive.IsConfigured)
            return Problem("La integración con Google Drive no está configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);

        var entity = await FindCase(id);
        if (entity is null) return NotFound();

        try
        {
            await EnsureDriveFolderAsync(entity);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(new DriveFolderDto(entity.DriveFolderId!, DriveFolderUrl(entity.DriveFolderId!)));
    }

    [HttpGet("{id:int}/drive-files")]
    public async Task<ActionResult<IEnumerable<DriveFileDto>>> GetDriveFiles(int id)
    {
        var entity = await FindCase(id);
        if (entity is null) return NotFound();
        if (string.IsNullOrWhiteSpace(entity.DriveFolderId))
            return Ok(Array.Empty<DriveFileDto>());

        if (!_drive.IsConfigured)
            return Problem("La integración con Google Drive no está configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);

        var files = await _drive.ListFilesAsync(entity.DriveFolderId);
        return Ok(files.Select(f => new DriveFileDto(f.Id, f.Name, f.MimeType, f.WebViewLink, f.CreatedAt, f.Size)));
    }

    // Crea la carpeta del caso si aún no existe (primer documento) y sube el
    // archivo ahí. Límite generoso pero evita subidas descontroladas.
    [HttpPost("{id:int}/drive-files")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<DriveFileDto>> UploadDriveFile(int id, IFormFile file)
    {
        if (!_drive.IsConfigured)
            return Problem("La integración con Google Drive no está configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Archivo requerido" });

        var entity = await FindCase(id);
        if (entity is null) return NotFound();

        try
        {
            await EnsureDriveFolderAsync(entity);

            await using var stream = file.OpenReadStream();
            var uploaded = await _drive.UploadFileAsync(entity.DriveFolderId!, file.FileName, file.ContentType, stream);

            return Ok(new DriveFileDto(uploaded.Id, uploaded.Name, uploaded.MimeType, uploaded.WebViewLink, uploaded.CreatedAt, uploaded.Size));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task EnsureDriveFolderAsync(Case entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.DriveFolderId)) return;

        var defendant = entity.Parties.FirstOrDefault(p => p.ProcessRole == "DEMANDADO");
        var defendantName = defendant?.Person?.Split('|').FirstOrDefault()?.Trim();
        var label = !string.IsNullOrWhiteSpace(entity.Process.Radicado) ? entity.Process.Radicado : $"Caso-{entity.Id}";
        var folderName = string.IsNullOrWhiteSpace(defendantName) ? label : $"{label} - {defendantName}";

        entity.DriveFolderId = await _drive.CreateCaseFolderAsync(folderName);
        await _db.SaveChangesAsync();
    }

    private static string DriveFolderUrl(string folderId) => $"https://drive.google.com/drive/folders/{folderId}";

    // ── helpers ───────────────────────────────────────────────────────────

    private IQueryable<Case> QueryWithIncludes() =>
        _db.Cases
            .Include(c => c.DemandanteRole)
            .Include(c => c.Parties)
            .Include(c => c.ProcessStages)
            .Include(c => c.ProceduralNotes);

    public readonly record struct AuthContext(bool IsAdmin, bool HasAnyDemandanteRole, string[] DemandanteIds);

    // Se resuelve contra la BD en cada request (no contra los claims horneados en
    // el JWT al loguear) para que activar/desactivar un rol-demandante, o
    // reasignarlo a un usuario, tenga efecto inmediato sin esperar a un nuevo
    // login. r-admin sigue viniendo del claim de rol del JWT (igual que en el
    // resto de los controllers) — solo la restricción por demandante es en vivo.
    //
    // HasAnyDemandanteRole distingue "el usuario no tiene ningún rol-demandante
    // (ej. r-user genérico) => sin restricción" de "tiene uno pero está inactivo
    // => debe quedar SIN ver ningún caso". Si solo mirásemos si DemandanteIds
    // quedó vacío no podríamos distinguir ambos casos, y un rol-demandante
    // desactivado terminaría viendo TODOS los casos en vez de ninguno.
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

    // r-admin ve todos los casos. Un usuario con rol(es)-demandante solo ve los
    // casos de su(s) rol(es) activos (resuelto en vivo, ver arriba) — si todos
    // sus roles-demandante están inactivos, no ve ningún caso. Un usuario sin
    // ningún rol-demandante (ej. r-user genérico) no se filtra, preservando el
    // comportamiento previo para staff interno.
    private IQueryable<Case> AuthorizedQuery(AuthContext auth)
    {
        var query = QueryWithIncludes();

        if (auth.IsAdmin || !auth.HasAnyDemandanteRole)
            return query;

        if (auth.DemandanteIds.Length == 0)
            return query.Where(c => false);

        return query.Where(BuildDemandanteFilter(auth.DemandanteIds));
    }

    // OR explícito (en vez de demandanteIds.Contains(c.DemandanteRoleId)) por
    // consistencia con el resto del código — demandanteIds es siempre un
    // conjunto pequeño (1-2 elementos en la práctica).
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

    private static bool IsDemandanteAllowed(AuthContext auth, string demandanteRoleId)
    {
        if (auth.IsAdmin || !auth.HasAnyDemandanteRole) return true; // admin o ej. r-user genérico, sin restricción
        return auth.DemandanteIds.Contains(demandanteRoleId);
    }

    private async Task<Case?> FindCase(int id)
    {
        var auth = await GetAuthContextAsync();
        return await AuthorizedQuery(auth).FirstOrDefaultAsync(c => c.Id == id);
    }

    private static Case BuildEntity(CreateCaseRequest req) => new()
    {
        DemandanteRoleId = req.DemandanteRoleId,
        Process = MapProcess(req.Process),
        FinancialInfo = MapFinancial(req.FinancialInfo),
        Measures = MapMeasures(req.Measures),
        Stages = MapStages(req.Stages),
        Auction = MapAuction(req.Auction),
        Closure = MapClosure(req.Closure),
        Parties = req.PartiesInfo.Select(MapParty).ToList()
    };

    private static void ApplyUpdate(Case entity, UpdateCaseRequest req)
    {
        entity.DemandanteRoleId = req.DemandanteRoleId;

        entity.Process.Radicado = req.Process.Radicado?.Trim() ?? "";
        entity.Process.ProcessType = req.Process.ProcessType.Trim();
        entity.Process.Court = req.Process.Court.Trim();
        entity.Process.City = req.Process.City.Trim();
        entity.Process.FiledAt = req.Process.FiledAt;

        entity.FinancialInfo = MapFinancial(req.FinancialInfo);
        entity.Measures = MapMeasures(req.Measures);
        entity.Stages = MapStages(req.Stages);
        entity.Auction = MapAuction(req.Auction);
        entity.Closure = MapClosure(req.Closure);

        entity.Parties.Clear();
        foreach (var p in req.PartiesInfo)
            entity.Parties.Add(MapParty(p));
    }

    private static CaseProcess MapProcess(ProcessInfoDto d) => new()
    {
        Radicado = d.Radicado?.Trim() ?? "",
        ProcessType = d.ProcessType.Trim(),
        Court = d.Court.Trim(),
        City = d.City.Trim(),
        FiledAt = d.FiledAt
    };

    private static CaseFinancial? MapFinancial(FinancialInfoDto? d) =>
        d is null ? null : new() { Capital = d.Capital, Obligations = d.Obligations?.Trim(), FngFag = d.FngFag };

    private static CaseMeasures? MapMeasures(MeasuresInfoDto? d) =>
        d is null ? null : new() { Embargo = d.Embargo, EmbargoDate = d.EmbargoDate, RemanentEmbargo = d.RemanentEmbargo, RemanentEntity = d.RemanentEntity?.Trim() };

    private static CaseStages? MapStages(StagesInfoDto? d) =>
        d is null ? null : new() { PaymentOrder = d.PaymentOrder, PersonalNotification = d.PersonalNotification, PersonalNotificationDate = d.PersonalNotificationDate, FirstInstanceSentence = d.FirstInstanceSentence, FirstInstanceDate = d.FirstInstanceDate, SecondInstance = d.SecondInstance, SecondInstanceDate = d.SecondInstanceDate };

    private static CaseAuction? MapAuction(AuctionInfoDto? d) =>
        d is null ? null : new() { AppraisalStatus = d.AppraisalStatus, AppraisalDate = d.AppraisalDate, AppraisalValue = d.AppraisalValue, AuctionStatus = d.AuctionStatus, AuctionDate = d.AuctionDate, Awarded = d.Awarded, AwardDate = d.AwardDate };

    private static CaseClosure? MapClosure(ClosureInfoDto? d) =>
        d is null ? null : new() { TerminationDate = d.TerminationDate, TerminationReason = d.TerminationReason?.Trim(), TitlesStatus = d.TitlesStatus?.Trim(), Delivery = d.Delivery, DeliveryDate = d.DeliveryDate, FileReturnStatus = d.FileReturnStatus?.Trim(), FileReturnDate = d.FileReturnDate };

    private static CaseParty MapParty(PartyInfoDto d) => new() { Person = d.Person.Trim(), ProcessRole = d.ProcessRole.Trim() };

    private static CaseDto ToDto(Case c) => new(
        c.Id,
        c.CreatedAt.ToString("yyyy-MM-dd"),
        c.DemandanteRoleId,
        c.DemandanteRole?.Name,
        c.DriveFolderId,
        c.DriveFolderId is null ? null : DriveFolderUrl(c.DriveFolderId),
        new ProcessInfoDto(c.Process.Radicado, c.Process.ProcessType, c.Process.Court, c.Process.City, c.Process.FiledAt),
        c.Parties.Select(p => new PartyInfoDto(p.Person, p.ProcessRole)).ToList(),
        c.FinancialInfo is null ? null : new FinancialInfoDto(c.FinancialInfo.Capital, c.FinancialInfo.Obligations, c.FinancialInfo.FngFag),
        c.Measures is null ? null : new MeasuresInfoDto(c.Measures.Embargo, c.Measures.EmbargoDate, c.Measures.RemanentEmbargo, c.Measures.RemanentEntity),
        c.Stages is null ? null : new StagesInfoDto(c.Stages.PaymentOrder, c.Stages.PersonalNotification, c.Stages.PersonalNotificationDate, c.Stages.FirstInstanceSentence, c.Stages.FirstInstanceDate, c.Stages.SecondInstance, c.Stages.SecondInstanceDate),
        c.Auction is null ? null : new AuctionInfoDto(c.Auction.AppraisalStatus, c.Auction.AppraisalDate, c.Auction.AppraisalValue, c.Auction.AuctionStatus, c.Auction.AuctionDate, c.Auction.Awarded, c.Auction.AwardDate),
        c.Closure is null ? null : new ClosureInfoDto(c.Closure.TerminationDate, c.Closure.TerminationReason, c.Closure.TitlesStatus, c.Closure.Delivery, c.Closure.DeliveryDate, c.Closure.FileReturnStatus, c.Closure.FileReturnDate),
        c.ProcessStages.OrderBy(s => s.Id).Select(s => new ProcessStageDto(s.Id, s.CreatedAt, s.StageName, s.SubStageName, s.Observation)).ToList(),
        c.ProceduralNotes.OrderBy(n => n.Id).Select(n => new ProceduralNoteDto(n.Id, n.CreatedAt, n.Text)).ToList()
    );
}

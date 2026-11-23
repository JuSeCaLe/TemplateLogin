using ClosedXML.Excel;
using Login.Infrastructure.Data.Identity;
using Login.Infrastructure.Model;
using Login.Infrastructure.Model.Cases;
using Login.Infrastructure.Model.Catalog;
using Login.Infrastructure.Services;
using Login.WebApi.Controllers.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Login.WebApi.Controllers;

// Módulo admin de carga masiva de casos: descarga la plantilla .xlsx y sube
// el archivo lleno. La subida siempre corre en modo "vista previa" primero
// (commit=false, no toca la base de datos) para que el admin revise fila por
// fila qué se va a crear y qué quedó con error antes de confirmar — mismo
// archivo, mismo endpoint, solo cambia el query param.
[ApiController]
[Route("api/case-bulk-import")]
[Authorize(Roles = "r-admin")]
public class CaseBulkImportController : ControllerBase
{
    private const string CasesSheetName = "Casos";
    private const string StagesSheetName = "Etapas";

    private readonly DataContext _db;
    private readonly RoleManager<AppRole> _roleManager;

    public CaseBulkImportController(DataContext db, RoleManager<AppRole> roleManager)
    {
        _db = db;
        _roleManager = roleManager;
    }

    // ── Plantilla ────────────────────────────────────────────────────────

    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate()
    {
        var demandantes = await _roleManager.Roles
            .Where(r => r.IsDemandante && r.Active)
            .Select(r => r.Name!)
            .OrderBy(n => n)
            .ToListAsync();

        var tiposProceso = await _db.TiposProceso
            .Where(t => t.Active)
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToListAsync();

        var catalog = await _db.CatalogProcessTypes
            .Include(p => p.Stages).ThenInclude(s => s.SubStages)
            .OrderBy(p => p.Id)
            .ToListAsync();

        using var wb = new XLWorkbook();

        // ── Instrucciones ──
        var help = wb.Worksheets.Add("Instrucciones");
        var helpLines = new[]
        {
            "Cómo llenar esta plantilla",
            "",
            "Hoja \"Casos\": un caso por fila.",
            "  - N° Carpeta: recomendado, debe ser único en el archivo (sirve para enlazar las filas de la hoja Etapas).",
            "  - Demandante: debe coincidir EXACTO con uno de estos roles activos: " + string.Join(", ", demandantes),
            "  - Tipo de Proceso: debe coincidir EXACTO con uno de estos, ya creados en Paramétricas: " + string.Join(", ", tiposProceso),
            "  - Demandado, Cédula/NIT Demandado, Juzgado, Ciudad son obligatorios.",
            "  - Radicado, Fecha Presentación Demanda, Capital, Obligaciones, Avalista y Cédula Avalista son opcionales.",
            "  - Fechas en formato dd/mm/aaaa.",
            "",
            "Hoja \"Etapas\" (opcional): el historial de cada caso, una fila por etapa. Si un caso no tiene ninguna fila acá, se crea sin etapas.",
            "  - N° Carpeta: debe existir en la hoja Casos.",
            "  - Fecha en formato dd/mm/aaaa.",
            "  - Etapa y Subetapa deben coincidir EXACTO (no importan tildes ni mayúsculas) con las de la hoja \"Catálogo de Etapas\", según el Tipo de Proceso de ese caso. Subetapa puede ir vacía si la etapa no tiene.",
            "  - Si una etapa/subetapa no coincide con el catálogo, o la fecha no es válida, ESE CASO COMPLETO no se crea (se reporta el error al subir el archivo, no se crea a medias).",
            "",
            "Sube el archivo desde \"Casos → Carga masiva\". Primero se muestra una vista previa (nada se guarda todavía); revisa los errores, corrige el Excel si hace falta y vuelve a subirlo, y solo al confirmar se crean los casos."
        };
        for (var i = 0; i < helpLines.Length; i++) help.Cell(i + 1, 1).Value = helpLines[i];
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Column(1).Width = 130;
        help.Style.Alignment.WrapText = false;

        // ── Casos ──
        var casesSheet = wb.Worksheets.Add(CasesSheetName);
        string[] caseHeaders =
        [
            "N° Carpeta", "Demandante", "Demandado", "Cédula/NIT Demandado", "Tipo de Proceso",
            "Radicado", "Juzgado", "Ciudad", "Fecha Presentación Demanda", "Capital",
            "Obligaciones", "Avalista", "Cédula Avalista"
        ];
        for (var i = 0; i < caseHeaders.Length; i++) casesSheet.Cell(1, i + 1).Value = caseHeaders[i];
        casesSheet.Row(1).Style.Font.Bold = true;

        casesSheet.Cell(2, 1).Value = "311";
        casesSheet.Cell(2, 2).Value = demandantes.FirstOrDefault() ?? "BBVA";
        casesSheet.Cell(2, 3).Value = "Juan Pérez";
        casesSheet.Cell(2, 4).Value = "12345678";
        casesSheet.Cell(2, 5).Value = tiposProceso.FirstOrDefault() ?? "EJECUTIVO HIPOTECARIO";
        casesSheet.Cell(2, 6).Value = "41001310300420180026200";
        casesSheet.Cell(2, 7).Value = "JUZGADO 4° CIVIL DEL CIRCUITO DE NEIVA";
        casesSheet.Cell(2, 8).Value = "NEIVA";
        casesSheet.Cell(2, 9).Value = "09/10/2015";
        casesSheet.Cell(2, 10).Value = 100000000;
        casesSheet.Cell(2, 11).Value = "1234567890";

        if (demandantes.Count > 0)
        {
            var listStr = "\"" + string.Join(",", demandantes) + "\"";
            casesSheet.Range(2, 2, 500, 2).CreateDataValidation().List(listStr);
        }
        if (tiposProceso.Count > 0)
        {
            var listStr = "\"" + string.Join(",", tiposProceso) + "\"";
            casesSheet.Range(2, 5, 500, 5).CreateDataValidation().List(listStr);
        }
        casesSheet.Columns().AdjustToContents();

        // ── Etapas ──
        var stagesSheet = wb.Worksheets.Add(StagesSheetName);
        string[] stageHeaders = ["N° Carpeta", "Fecha", "Etapa", "Subetapa", "Observación"];
        for (var i = 0; i < stageHeaders.Length; i++) stagesSheet.Cell(1, i + 1).Value = stageHeaders[i];
        stagesSheet.Row(1).Style.Font.Bold = true;
        stagesSheet.Cell(2, 1).Value = "311";
        stagesSheet.Cell(2, 2).Value = "09/10/2015";
        stagesSheet.Cell(2, 3).Value = "DEMANDA";
        stagesSheet.Cell(2, 4).Value = "PRESENTACIÓN DEMANDA";
        stagesSheet.Cell(2, 5).Value = "";
        stagesSheet.Columns().AdjustToContents();

        // ── Catálogo de Etapas (referencia, copiar/pegar el texto exacto) ──
        var catalogSheet = wb.Worksheets.Add("Catálogo de Etapas");
        string[] catalogHeaders = ["Tipo de Proceso (agrupador)", "Etapa", "Subetapa"];
        for (var i = 0; i < catalogHeaders.Length; i++) catalogSheet.Cell(1, i + 1).Value = catalogHeaders[i];
        catalogSheet.Row(1).Style.Font.Bold = true;
        var row = 2;
        foreach (var pt in catalog)
        {
            foreach (var stage in pt.Stages)
            {
                if (stage.SubStages.Count == 0)
                {
                    catalogSheet.Cell(row, 1).Value = pt.Name;
                    catalogSheet.Cell(row, 2).Value = stage.Name;
                    catalogSheet.Cell(row, 3).Value = "";
                    row++;
                    continue;
                }
                foreach (var sub in stage.SubStages)
                {
                    catalogSheet.Cell(row, 1).Value = pt.Name;
                    catalogSheet.Cell(row, 2).Value = stage.Name;
                    catalogSheet.Cell(row, 3).Value = sub.Name;
                    row++;
                }
            }
        }
        catalogSheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Plantilla_Carga_Masiva_Casos.xlsx");
    }

    // ── Carga ────────────────────────────────────────────────────────────

    // commit=false (default): solo valida y devuelve el reporte, no guarda nada.
    // commit=true: la misma validación, y si un caso pasó, se guarda.
    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<CaseBulkImportReport>> Import(IFormFile file, [FromQuery] bool commit = false)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Archivo requerido." });
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "El archivo debe ser un .xlsx de Excel." });

        var demandanteRoles = await _roleManager.Roles.Where(r => r.IsDemandante && r.Active).ToListAsync();
        var tiposProceso = await _db.TiposProceso.Where(t => t.Active).ToListAsync();
        var catalog = await _db.CatalogProcessTypes
            .Include(p => p.Stages).ThenInclude(s => s.SubStages)
            .ToListAsync();
        var existingRadicados = new HashSet<string>(
            await _db.Cases.Select(c => c.Process.Radicado).Where(r => r != null && r != "").ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        using var stream = file.OpenReadStream();
        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            return BadRequest(new { message = "No se pudo leer el archivo. ¿Es un .xlsx válido?" });
        }
        using (wb)
        {
            if (!wb.TryGetWorksheet(CasesSheetName, out var casesSheet))
                return BadRequest(new { message = $"El archivo no tiene una hoja llamada \"{CasesSheetName}\"." });

            var caseRows = ReadCaseRows(casesSheet);
            var stageRowsByFolder = wb.TryGetWorksheet(StagesSheetName, out var stagesSheet)
                ? ReadStageRows(stagesSheet)
                : new Dictionary<string, List<StageRow>>(StringComparer.OrdinalIgnoreCase);

            var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenRadicados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<CaseBulkImportRowResult>();
            var toCreate = new List<Case>();

            foreach (var r in caseRows)
            {
                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(r.Demandado)) errors.Add("Demandado es obligatorio.");
                if (string.IsNullOrWhiteSpace(r.DemandadoCedula)) errors.Add("Cédula/NIT del demandado es obligatorio.");
                if (string.IsNullOrWhiteSpace(r.Juzgado)) errors.Add("Juzgado es obligatorio.");
                if (string.IsNullOrWhiteSpace(r.Ciudad)) errors.Add("Ciudad es obligatorio.");

                if (!string.IsNullOrWhiteSpace(r.FolderNumber) && !seenFolders.Add(r.FolderNumber))
                    errors.Add($"N° Carpeta \"{r.FolderNumber}\" está repetido en el archivo.");

                AppRole? demandanteRole = null;
                if (string.IsNullOrWhiteSpace(r.Demandante))
                {
                    errors.Add("Demandante es obligatorio.");
                }
                else
                {
                    demandanteRole = demandanteRoles.FirstOrDefault(d => SameText(d.Name, r.Demandante));
                    if (demandanteRole is null)
                        errors.Add($"Demandante \"{r.Demandante}\" no coincide con ningún rol-demandante activo.");
                }

                Login.Infrastructure.Model.Parametros.TipoProceso? tipoProcesoMatch = null;
                if (string.IsNullOrWhiteSpace(r.TipoProceso))
                {
                    errors.Add("Tipo de Proceso es obligatorio.");
                }
                else
                {
                    tipoProcesoMatch = tiposProceso.FirstOrDefault(t => SameText(t.Name, r.TipoProceso));
                    if (tipoProcesoMatch is null)
                        errors.Add($"Tipo de Proceso \"{r.TipoProceso}\" no existe en Paramétricas.");
                }

                if (!string.IsNullOrWhiteSpace(r.Radicado))
                {
                    if (existingRadicados.Contains(r.Radicado) || !seenRadicados.Add(r.Radicado))
                        errors.Add($"Radicado \"{r.Radicado}\" ya existe (en la base de datos o repetido en el archivo).");
                }

                // Etapas asociadas a este caso (por N° Carpeta) — se validan
                // contra el catálogo del Tipo de Proceso de la fila.
                var stages = new List<CaseProcessStage>();
                if (!string.IsNullOrWhiteSpace(r.FolderNumber) &&
                    stageRowsByFolder.TryGetValue(r.FolderNumber, out var stageRows))
                {
                    var catalogName = MapToCatalogName(tipoProcesoMatch?.Name ?? r.TipoProceso ?? "");
                    var processCatalog = catalog.FirstOrDefault(c => SameText(c.Name, catalogName));

                    foreach (var sr in stageRows)
                    {
                        if (sr.Date is null)
                        {
                            errors.Add($"Etapas fila {sr.RowNumber}: fecha inválida o vacía.");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(sr.Stage))
                        {
                            errors.Add($"Etapas fila {sr.RowNumber}: la Etapa es obligatoria.");
                            continue;
                        }

                        var stageMatch = processCatalog?.Stages.FirstOrDefault(s => SameText(s.Name, sr.Stage));
                        if (stageMatch is null)
                        {
                            errors.Add($"Etapas fila {sr.RowNumber}: la etapa \"{sr.Stage}\" no existe en el catálogo de {catalogName}.");
                            continue;
                        }

                        string subStageName = "";
                        if (!string.IsNullOrWhiteSpace(sr.SubStage))
                        {
                            var subMatch = stageMatch.SubStages.FirstOrDefault(ss => SameText(ss.Name, sr.SubStage));
                            if (subMatch is null)
                            {
                                errors.Add($"Etapas fila {sr.RowNumber}: la subetapa \"{sr.SubStage}\" no existe bajo \"{stageMatch.Name}\".");
                                continue;
                            }
                            subStageName = subMatch.Name;
                        }

                        stages.Add(new CaseProcessStage
                        {
                            CreatedAt = sr.Date.Value.ToString("yyyy-MM-dd"),
                            StageName = stageMatch.Name,
                            SubStageName = subStageName,
                            Observation = string.IsNullOrWhiteSpace(sr.Observation) ? null : sr.Observation!.Trim()
                        });
                    }
                }

                if (errors.Count > 0)
                {
                    results.Add(new CaseBulkImportRowResult(r.RowNumber, r.FolderNumber, r.Demandado ?? "", false, errors, 0));
                    continue;
                }

                var parties = new List<CaseParty>
                {
                    new() { Person = $"{r.Demandado!.Trim()}|CC|{r.DemandadoCedula!.Trim()}", ProcessRole = "DEMANDADO" }
                };
                if (!string.IsNullOrWhiteSpace(r.Avalista))
                    parties.Add(new CaseParty { Person = $"{r.Avalista.Trim()}|CC|{r.AvalistaCedula?.Trim() ?? ""}", ProcessRole = "AVALISTA" });

                var entity = new Case
                {
                    DemandanteRoleId = demandanteRole!.Id,
                    FolderNumber = string.IsNullOrWhiteSpace(r.FolderNumber) ? null : r.FolderNumber.Trim(),
                    Process = new CaseProcess
                    {
                        Radicado = r.Radicado?.Trim() ?? "",
                        ProcessType = tipoProcesoMatch!.Name,
                        Court = r.Juzgado!.Trim(),
                        City = r.Ciudad!.Trim(),
                        FiledAt = r.FiledAt?.ToString("yyyy-MM-dd")
                    },
                    FinancialInfo = (r.Capital is not null || !string.IsNullOrWhiteSpace(r.Obligations))
                        ? new CaseFinancial { Capital = r.Capital, Obligations = string.IsNullOrWhiteSpace(r.Obligations) ? null : r.Obligations.Trim() }
                        : null,
                    Parties = parties,
                    ProcessStages = stages
                };

                toCreate.Add(entity);
                results.Add(new CaseBulkImportRowResult(r.RowNumber, r.FolderNumber, r.Demandado ?? "", true, [], stages.Count));
            }

            if (commit && toCreate.Count > 0)
            {
                _db.Cases.AddRange(toCreate);
                await _db.SaveChangesAsync();
            }

            return Ok(new CaseBulkImportReport(
                commit,
                caseRows.Count,
                toCreate.Count,
                caseRows.Count - toCreate.Count,
                results));
        }
    }

    // ── parsing ──────────────────────────────────────────────────────────

    private record CaseRow(
        int RowNumber, string? FolderNumber, string? Demandante, string? Demandado, string? DemandadoCedula,
        string? TipoProceso, string? Radicado, string? Juzgado, string? Ciudad, DateTime? FiledAt,
        decimal? Capital, string? Obligations, string? Avalista, string? AvalistaCedula);

    private record StageRow(int RowNumber, string FolderNumber, DateTime? Date, string? Stage, string? SubStage, string? Observation);

    private static List<CaseRow> ReadCaseRows(IXLWorksheet sheet)
    {
        var rows = new List<CaseRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var i = 2; i <= lastRow; i++)
        {
            var r = sheet.Row(i);
            if (r.IsEmpty()) continue;

            rows.Add(new CaseRow(
                RowNumber: i,
                FolderNumber: Str(r.Cell(1)),
                Demandante: Str(r.Cell(2)),
                Demandado: Str(r.Cell(3)),
                DemandadoCedula: Str(r.Cell(4)),
                TipoProceso: Str(r.Cell(5)),
                Radicado: Str(r.Cell(6)),
                Juzgado: Str(r.Cell(7)),
                Ciudad: Str(r.Cell(8)),
                FiledAt: DateOf(r.Cell(9)),
                Capital: DecimalOf(r.Cell(10)),
                Obligations: Str(r.Cell(11)),
                Avalista: Str(r.Cell(12)),
                AvalistaCedula: Str(r.Cell(13))
            ));
        }
        return rows;
    }

    private static Dictionary<string, List<StageRow>> ReadStageRows(IXLWorksheet sheet)
    {
        var byFolder = new Dictionary<string, List<StageRow>>(StringComparer.OrdinalIgnoreCase);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var i = 2; i <= lastRow; i++)
        {
            var r = sheet.Row(i);
            if (r.IsEmpty()) continue;

            var folder = Str(r.Cell(1));
            if (string.IsNullOrWhiteSpace(folder)) continue;

            var stageRow = new StageRow(i, folder, DateOf(r.Cell(2)), Str(r.Cell(3)), Str(r.Cell(4)), Str(r.Cell(5)));
            if (!byFolder.TryGetValue(folder, out var list))
            {
                list = [];
                byFolder[folder] = list;
            }
            list.Add(stageRow);
        }
        return byFolder;
    }

    private static string? Str(IXLCell cell)
    {
        var s = cell.GetString().Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static DateTime? DateOf(IXLCell cell)
    {
        if (cell.TryGetValue(out DateTime dt)) return dt;
        var s = cell.GetString().Trim();
        if (string.IsNullOrEmpty(s)) return null;
        return DateTime.TryParse(s, System.Globalization.CultureInfo.GetCultureInfo("es-CO"), System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static decimal? DecimalOf(IXLCell cell)
    {
        if (cell.TryGetValue(out decimal d)) return d;
        var s = cell.GetString().Trim();
        return decimal.TryParse(s, out var parsed) ? parsed : null;
    }

    // Mismo criterio que el frontend (process-stage-dialog.ts mapToCatalogName):
    // el Tipo de Proceso de la fila no tiene que llamarse igual que el
    // catálogo de etapas, se agrupa por palabra clave.
    private static string MapToCatalogName(string processType)
    {
        var pt = processType.ToUpperInvariant();
        if (pt.Contains("INSOLVENCIA")) return "INSOLVENCIA";
        if (pt.Contains("RESTITU")) return "RESTITUCIÓN";
        return "HIPOTECARIO";
    }

    private static bool SameText(string a, string b) =>
        MemorialGenerationService.NormalizeKey(a) == MemorialGenerationService.NormalizeKey(b);
}

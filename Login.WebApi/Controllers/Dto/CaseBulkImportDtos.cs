namespace Login.WebApi.Controllers.Dto;

public record CaseBulkImportRowResult(
    int RowNumber,          // fila en la hoja "Casos" (empezando en 2, después del encabezado)
    string? FolderNumber,
    string Demandado,
    bool Ok,
    List<string> Errors,
    int StagesCount
);

public record CaseBulkImportReport(
    bool Committed,         // false = solo vista previa, true = ya se guardó en la base de datos
    int TotalCases,
    int ValidCases,
    int InvalidCases,
    List<CaseBulkImportRowResult> Rows
);

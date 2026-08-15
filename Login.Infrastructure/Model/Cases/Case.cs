using Login.Infrastructure.Data.Identity;

namespace Login.Infrastructure.Model.Cases;

public class Case
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string DemandanteRoleId { get; set; } = null!;
    public AppRole DemandanteRole { get; set; } = null!;

    // Carpeta de Google Drive del caso (creada por la app, no editable a
    // mano). Null hasta que se crea la carpeta desde CaseEdit.
    public string? DriveFolderId { get; set; }

    public CaseProcess Process { get; set; } = new();
    public CaseFinancial? FinancialInfo { get; set; }
    public CaseMeasures? Measures { get; set; }
    public CaseStages? Stages { get; set; }
    public CaseAuction? Auction { get; set; }
    public CaseClosure? Closure { get; set; }

    public List<CaseParty> Parties { get; set; } = [];
    public List<CaseProcessStage> ProcessStages { get; set; } = [];
    public List<CaseProceduralNote> ProceduralNotes { get; set; } = [];
}

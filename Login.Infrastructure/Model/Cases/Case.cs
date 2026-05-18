namespace Login.Infrastructure.Model.Cases;

public class Case
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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

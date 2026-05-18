namespace Login.Infrastructure.Model.Cases;

public class CaseProcessStage
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public Case Case { get; set; } = null!;
    public string CreatedAt { get; set; } = "";
    public string StageName { get; set; } = "";
    public string SubStageName { get; set; } = "";
    public string? Observation { get; set; }
}

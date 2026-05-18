namespace Login.Infrastructure.Model.Cases;

public class CaseProceduralNote
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public Case Case { get; set; } = null!;
    public string CreatedAt { get; set; } = "";
    public string Text { get; set; } = "";
}

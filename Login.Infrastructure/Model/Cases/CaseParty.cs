namespace Login.Infrastructure.Model.Cases;

public class CaseParty
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public Case Case { get; set; } = null!;
    public string Person { get; set; } = "";
    public string ProcessRole { get; set; } = "";
}

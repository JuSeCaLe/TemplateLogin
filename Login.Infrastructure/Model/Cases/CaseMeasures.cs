namespace Login.Infrastructure.Model.Cases;

public class CaseMeasures
{
    public bool Embargo { get; set; }
    public string? EmbargoDate { get; set; }
    public bool RemanentEmbargo { get; set; }
    public string? RemanentEntity { get; set; }
}

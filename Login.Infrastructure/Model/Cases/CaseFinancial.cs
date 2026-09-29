namespace Login.Infrastructure.Model.Cases;

public class CaseFinancial
{
    public decimal? Capital { get; set; }
    public string? Obligations { get; set; }
    public bool FngFag { get; set; }
    public bool PaymentAgreement { get; set; }
    public bool PortfolioSale { get; set; }
}

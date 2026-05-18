namespace Login.Infrastructure.Model.Cases;

public class CaseAuction
{
    public string? AppraisalStatus { get; set; }
    public string? AppraisalDate { get; set; }
    public decimal? AppraisalValue { get; set; }
    public string? AuctionStatus { get; set; }
    public string? AuctionDate { get; set; }
    public bool? Awarded { get; set; }
    public string? AwardDate { get; set; }
}

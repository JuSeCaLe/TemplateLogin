namespace Login.Infrastructure.Model.Cases;

public class CaseClosure
{
    public string? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string? TitlesStatus { get; set; }
    public bool? Delivery { get; set; }
    public string? DeliveryDate { get; set; }
    public string? FileReturnStatus { get; set; }
    public string? FileReturnDate { get; set; }
}

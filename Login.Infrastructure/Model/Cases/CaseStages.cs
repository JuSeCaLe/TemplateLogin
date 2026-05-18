namespace Login.Infrastructure.Model.Cases;

public class CaseStages
{
    public bool PaymentOrder { get; set; }
    public bool PersonalNotification { get; set; }
    public string? PersonalNotificationDate { get; set; }
    public bool FirstInstanceSentence { get; set; }
    public string? FirstInstanceDate { get; set; }
    public bool SecondInstance { get; set; }
    public string? SecondInstanceDate { get; set; }
}

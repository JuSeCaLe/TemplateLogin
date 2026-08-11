namespace Login.Infrastructure.Model.Cases;

public class CaseProcess
{
    public string Radicado { get; set; } = "";
    public string ProcessType { get; set; } = "";
    public string Court { get; set; } = "";
    public string City { get; set; } = "";
    // Fecha de presentación de la demanda, como texto "yyyy-MM-dd" (mismo
    // criterio que las demás fechas del modelo, ej. Measures.EmbargoDate).
    public string? FiledAt { get; set; }
}

namespace Login.Infrastructure.Model;

// Plantilla de memorial (.docx con placeholders {{JUZGADO}}, {{CIUDAD}},
// {{TIPO_PROCESO}}, {{DEMANDANTE}}, {{DEMANDADO}}, {{DEMANDADO_CEDULA}},
// {{RADICADO}}), guardada en disco bajo Templates/Memoriales/FileName.
// Ver MemorialGenerationService para el motor de reemplazo.
public class MemorialTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

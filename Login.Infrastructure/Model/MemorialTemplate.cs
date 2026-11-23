namespace Login.Infrastructure.Model;

// Plantilla de memorial (.docx) subida desde el módulo admin. El contenido
// del archivo vive en Content (no en disco) para que crear/reemplazar sea
// una operación de base de datos, sin depender de la carpeta de la
// instalación (que puede no sobrevivir una reinstalación/actualización).
//
// El texto variable va entre corchetes, ej. [TipoProceso], o con el formato
// legado {{TIPO_PROCESO}} de las plantillas creadas antes de este módulo —
// ambos se soportan (ver MemorialGenerationService). DetectedPlaceholders
// guarda los nombres encontrados la última vez que se subió/reemplazó el
// archivo, solo para mostrarlos en el admin (no se usa al generar).
public class MemorialTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    // Nombre original del archivo subido (solo informativo / para el
    // download; ya no se usa para ubicar el archivo en disco).
    public string FileName { get; set; } = "";

    // Null hasta que se sube el archivo por primera vez (no debería pasar en
    // la práctica, Create siempre lo exige) o, en una plantilla heredada de
    // antes de este módulo, hasta que arranca el backend una vez y hace el
    // backfill desde disco (ver Program.cs).
    public byte[]? Content { get; set; }
    public string DetectedPlaceholders { get; set; } = "";

    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

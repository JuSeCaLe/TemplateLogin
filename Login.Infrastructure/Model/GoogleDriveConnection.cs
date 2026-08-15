namespace Login.Infrastructure.Model;

// Fila única: la cuenta de Google (Gmail personal, no Workspace) autorizada
// una sola vez por un admin. Todos los documentos de todos los casos, sin
// importar qué usuario de la App los suba, viven en el Drive de esta cuenta.
public class GoogleDriveConnection
{
    public int Id { get; set; }
    public string RefreshToken { get; set; } = "";
    public string? ConnectedEmail { get; set; }
    public DateTime ConnectedAt { get; set; }

    // Carpeta raíz ("AbogApp2 - Casos") dentro del Drive de la cuenta
    // conectada. La crea la propia app al conectar — con scope drive.file
    // no puede usar una carpeta preexistente que ella misma no haya creado.
    public string? RootFolderId { get; set; }
}

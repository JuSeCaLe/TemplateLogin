using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.Extensions.Options;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Login.Infrastructure.Services;

public class GoogleDriveOptions
{
    // Contenido completo del JSON de la cuenta de servicio (no una ruta de
    // archivo) — se guarda vía user-secrets/variables de entorno, nunca en
    // el repo. Ver Program.cs / appsettings.json.
    public string? ServiceAccountJson { get; set; }

    // Id de la carpeta raíz de Drive (compartida con la cuenta de servicio)
    // dentro de la cual se crea una subcarpeta por caso.
    public string? RootFolderId { get; set; }
}

public record DriveFileInfo(string Id, string Name, string? MimeType, string? WebViewLink, DateTime? CreatedAt, long? Size);

// Envuelve la Google Drive API v3. Un solo folder raíz compartido con la
// cuenta de servicio; cada Case tiene (o no) una subcarpeta ahí (Case.DriveFolderId).
public class GoogleDriveService
{
    private readonly GoogleDriveOptions _options;
    private DriveService? _client;

    public GoogleDriveService(IOptions<GoogleDriveOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ServiceAccountJson) &&
        !string.IsNullOrWhiteSpace(_options.RootFolderId);

    private DriveService GetClient()
    {
        if (_client is not null) return _client;

        if (string.IsNullOrWhiteSpace(_options.ServiceAccountJson))
            throw new InvalidOperationException("Google Drive no está configurado (falta GoogleDrive:ServiceAccountJson).");

        var credential = GoogleCredential
            .FromJson(_options.ServiceAccountJson)
            .CreateScoped(DriveService.ScopeConstants.Drive);

        _client = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "AbogApp2"
        });

        return _client;
    }

    public async Task<string> CreateCaseFolderAsync(string folderName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.RootFolderId))
            throw new InvalidOperationException("Google Drive no está configurado (falta GoogleDrive:RootFolderId).");

        var client = GetClient();
        var metadata = new DriveFile
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = [_options.RootFolderId]
        };

        var request = client.Files.Create(metadata);
        request.Fields = "id";
        request.SupportsAllDrives = true;

        var folder = await request.ExecuteAsync(ct);
        return folder.Id;
    }

    public async Task<IReadOnlyList<DriveFileInfo>> ListFilesAsync(string folderId, CancellationToken ct = default)
    {
        var client = GetClient();
        var request = client.Files.List();
        request.Q = $"'{folderId}' in parents and trashed = false";
        request.Fields = "files(id, name, mimeType, webViewLink, createdTime, size)";
        request.OrderBy = "createdTime desc";
        request.SupportsAllDrives = true;
        request.IncludeItemsFromAllDrives = true;

        var result = await request.ExecuteAsync(ct);
        return result.Files
            .Select(f => new DriveFileInfo(f.Id, f.Name, f.MimeType, f.WebViewLink, f.CreatedTimeDateTimeOffset?.UtcDateTime, f.Size))
            .ToList();
    }

    public async Task<DriveFileInfo> UploadFileAsync(string folderId, string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        var client = GetClient();
        var metadata = new DriveFile
        {
            Name = fileName,
            Parents = [folderId]
        };

        var request = client.Files.Create(metadata, content, contentType);
        request.Fields = "id, name, mimeType, webViewLink, createdTime, size";
        request.SupportsAllDrives = true;

        var progress = await request.UploadAsync(ct);
        if (progress.Status != UploadStatus.Completed)
            throw new InvalidOperationException($"Error subiendo archivo a Drive: {progress.Exception?.Message}");

        var f = request.ResponseBody;
        return new DriveFileInfo(f.Id, f.Name, f.MimeType, f.WebViewLink, f.CreatedTimeDateTimeOffset?.UtcDateTime, f.Size);
    }
}

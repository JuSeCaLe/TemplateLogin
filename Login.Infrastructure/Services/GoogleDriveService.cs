using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Login.Infrastructure.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Login.Infrastructure.Services;

public class GoogleDriveOptions
{
    // Credenciales del cliente OAuth "Web application" (Google Cloud Console
    // → Credenciales). No son secretos de una cuenta de servicio: son los
    // que identifican a AbogApp2 ante Google para pedirle permiso a un usuario.
    public string? OAuthClientId { get; set; }
    public string? OAuthClientSecret { get; set; }
}

public record DriveFileInfo(string Id, string Name, string? MimeType, string? WebViewLink, DateTime? CreatedAt, long? Size);

// Envuelve la Google Drive API v3, usando el refresh_token de la única
// cuenta de Google conectada (ver GoogleDriveConnection) en vez de una
// cuenta de servicio — las cuentas de servicio no tienen cupo de
// almacenamiento propio y no pueden subir archivos.
public class GoogleDriveService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string Scope = "openid email https://www.googleapis.com/auth/drive.file";

    private readonly GoogleDriveOptions _options;
    private readonly DataContext _db;

    public GoogleDriveService(IOptions<GoogleDriveOptions> options, DataContext db)
    {
        _options = options.Value;
        _db = db;
    }

    private const string RootFolderName = "AbogApp2 - Casos";

    private bool OAuthClientConfigured =>
        !string.IsNullOrWhiteSpace(_options.OAuthClientId) &&
        !string.IsNullOrWhiteSpace(_options.OAuthClientSecret);

    // true si además de tener client id/secret configurados, ya hay una
    // cuenta de Google conectada con refresh_token guardado.
    public async Task<bool> IsReadyAsync(CancellationToken ct = default) =>
        OAuthClientConfigured && await _db.GoogleDriveConnections.AnyAsync(ct);

    public async Task<GoogleDriveConnection?> GetConnectionAsync(CancellationToken ct = default) =>
        await _db.GoogleDriveConnections.FirstOrDefaultAsync(ct);

    // ── OAuth: conectar la cuenta ──────────────────────────────────────────

    public string BuildAuthorizationUrl(string redirectUri, string state)
    {
        if (!OAuthClientConfigured)
            throw new InvalidOperationException("Google Drive no está configurado (falta GoogleDrive:OAuthClientId/OAuthClientSecret).");

        return AuthEndpoint +
            $"?client_id={Uri.EscapeDataString(_options.OAuthClientId!)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(Scope)}" +
            "&access_type=offline" +
            "&prompt=consent" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    // Intercambia el code por tokens y guarda (o reemplaza) la conexión.
    // Devuelve el email de la cuenta conectada.
    public async Task<string> ExchangeCodeAndSaveAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        var tokenJson = await PostFormAsync(new Dictionary<string, string>
        {
            ["client_id"] = _options.OAuthClientId!,
            ["client_secret"] = _options.OAuthClientSecret!,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        }, ct);

        var refreshToken = tokenJson.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var idToken = tokenJson.TryGetProperty("id_token", out var it) ? it.GetString() : null;

        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException(
                "Google no devolvió un refresh_token (puede pasar si la cuenta ya había autorizado la app antes). " +
                "Revoca el acceso en https://myaccount.google.com/permissions y vuelve a intentar.");

        var email = ExtractEmailFromIdToken(idToken);

        var existing = await _db.GoogleDriveConnections.FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            existing = new GoogleDriveConnection();
            _db.GoogleDriveConnections.Add(existing);
        }
        existing.RefreshToken = refreshToken!;
        existing.ConnectedEmail = email;
        existing.ConnectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // La carpeta raíz la crea la propia app (con este mismo token recién
        // obtenido) — con scope drive.file no hay forma de "compartirle" una
        // carpeta preexistente como se hacía con la cuenta de servicio.
        existing.RootFolderId = await FindOrCreateRootFolderAsync(ct);
        await _db.SaveChangesAsync(ct);

        return email ?? "(cuenta conectada)";
    }

    private async Task<string> FindOrCreateRootFolderAsync(CancellationToken ct)
    {
        var client = await GetClientAsync(ct);

        var search = client.Files.List();
        search.Q = $"name = '{RootFolderName}' and mimeType = 'application/vnd.google-apps.folder' and 'root' in parents and trashed = false";
        search.Fields = "files(id)";
        var existingFolders = await search.ExecuteAsync(ct);
        if (existingFolders.Files.Count > 0)
            return existingFolders.Files[0].Id;

        var metadata = new DriveFile
        {
            Name = RootFolderName,
            MimeType = "application/vnd.google-apps.folder"
        };
        var create = client.Files.Create(metadata);
        create.Fields = "id";
        var created = await create.ExecuteAsync(ct);
        return created.Id;
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var existing = await _db.GoogleDriveConnections.ToListAsync(ct);
        if (existing.Count == 0) return;
        _db.GoogleDriveConnections.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);
    }

    // ── Drive API ────────────────────────────────────────────────────────

    public async Task<string> CreateCaseFolderAsync(string folderName, CancellationToken ct = default)
    {
        var conn = await _db.GoogleDriveConnections.FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No hay ninguna cuenta de Google Drive conectada. Un administrador debe conectarla primero.");
        if (string.IsNullOrWhiteSpace(conn.RootFolderId))
            throw new InvalidOperationException("La cuenta conectada no tiene carpeta raíz creada todavía. Reconecta la cuenta desde Seguridad → Google Drive.");

        var client = await GetClientAsync(ct);
        var metadata = new DriveFile
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = [conn.RootFolderId]
        };

        var request = client.Files.Create(metadata);
        request.Fields = "id";

        var folder = await request.ExecuteAsync(ct);
        return folder.Id;
    }

    public async Task<IReadOnlyList<DriveFileInfo>> ListFilesAsync(string folderId, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var request = client.Files.List();
        request.Q = $"'{folderId}' in parents and trashed = false";
        request.Fields = "files(id, name, mimeType, webViewLink, createdTime, size)";
        request.OrderBy = "createdTime desc";

        var result = await request.ExecuteAsync(ct);
        return result.Files
            .Select(f => new DriveFileInfo(f.Id, f.Name, f.MimeType, f.WebViewLink, f.CreatedTimeDateTimeOffset?.UtcDateTime, f.Size))
            .ToList();
    }

    public async Task<DriveFileInfo> UploadFileAsync(string folderId, string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var metadata = new DriveFile
        {
            Name = fileName,
            Parents = [folderId]
        };

        var request = client.Files.Create(metadata, content, contentType);
        request.Fields = "id, name, mimeType, webViewLink, createdTime, size";

        var progress = await request.UploadAsync(ct);
        if (progress.Status != UploadStatus.Completed)
            throw new InvalidOperationException($"Error subiendo archivo a Drive: {progress.Exception?.Message}");

        var f = request.ResponseBody;
        return new DriveFileInfo(f.Id, f.Name, f.MimeType, f.WebViewLink, f.CreatedTimeDateTimeOffset?.UtcDateTime, f.Size);
    }

    // ── internals ────────────────────────────────────────────────────────

    private async Task<DriveService> GetClientAsync(CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(ct);
        var credential = GoogleCredential.FromAccessToken(accessToken);

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "AbogApp2"
        });
    }

    // Cambia el refresh_token guardado por un access_token fresco. Se pide
    // uno nuevo en cada operación en vez de cachear (volumen bajo, evita
    // manejar expiración a mano).
    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (!OAuthClientConfigured)
            throw new InvalidOperationException("Google Drive no está configurado (falta GoogleDrive:OAuthClientId/OAuthClientSecret).");

        var conn = await _db.GoogleDriveConnections.FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No hay ninguna cuenta de Google Drive conectada. Un administrador debe conectarla primero.");

        var json = await PostFormAsync(new Dictionary<string, string>
        {
            ["client_id"] = _options.OAuthClientId!,
            ["client_secret"] = _options.OAuthClientSecret!,
            ["refresh_token"] = conn.RefreshToken,
            ["grant_type"] = "refresh_token"
        }, ct, "No se pudo renovar el acceso a Google Drive");

        return json.GetProperty("access_token").GetString()!;
    }

    private static async Task<JsonElement> PostFormAsync(Dictionary<string, string> form, CancellationToken ct, string? errorPrefix = null)
    {
        using var http = new HttpClient();
        using var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{errorPrefix ?? "Error al comunicarse con Google"}: {body}");

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string? ExtractEmailFromIdToken(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;

        var parts = idToken.Split('.');
        if (parts.Length < 2) return null;

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload += (payload.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };

        var bytes = Convert.FromBase64String(payload);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
    }
}

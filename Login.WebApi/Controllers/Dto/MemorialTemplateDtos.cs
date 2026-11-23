namespace Login.WebApi.Controllers.Dto;

public record MemorialTemplateAdminDto(
    int Id,
    string Name,
    string FileName,
    bool Active,
    string CreatedAt,
    string UpdatedAt,
    List<string> Placeholders
);

public record UpdateMemorialTemplateRequest(string Name, bool Active);

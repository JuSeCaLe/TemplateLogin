namespace Login.WebApi.Controllers.Dto
{
    public record ParametricDto(Guid Id, string Name, string? Description, bool Active, DateTime CreatedAt);
    public record CreateParametricRequest(string Name, string? Description, bool Active);
    public record UpdateParametricRequest(string Name, string? Description, bool Active);

    public record CourtDto(Guid Id, string Name, string? Description, bool Active, DateTime CreatedAt, string City) : ParametricDto(Id, Name, Description, Active, CreatedAt);
    public record CreateCourtRequest(string Name, string? Description, bool Active, string City) : CreateParametricRequest(Name, Description, Active);
    public record UpdateCourtRequest(string Name, string? Description, bool Active, string City) : UpdateParametricRequest(Name, Description, Active);
}

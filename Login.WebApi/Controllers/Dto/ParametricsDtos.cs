namespace Login.WebApi.Controllers.Dto
{
    public record ParametricDto(Guid Id, string Name, string? Description, bool Active, DateTime CreatedAt);
    public record CreateParametricRequest(string Name, string? Description, bool Active);
    public record UpdateParametricRequest(string Name, string? Description, bool Active);
}

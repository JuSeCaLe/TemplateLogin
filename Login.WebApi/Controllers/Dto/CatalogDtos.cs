namespace Login.WebApi.Controllers.Dto;

public record CatalogSubStageDto(int Id, string Name);
public record CatalogStageDto(int Id, string Name, List<CatalogSubStageDto> SubStages);
public record CatalogProcessTypeDto(int Id, string Name, List<CatalogStageDto> Stages);

namespace Login.Infrastructure.Model.Catalog;

public class CatalogSubStage
{
    public int Id { get; set; }
    public int CatalogStageId { get; set; }
    public CatalogStage Stage { get; set; } = null!;
    public string Name { get; set; } = "";
}

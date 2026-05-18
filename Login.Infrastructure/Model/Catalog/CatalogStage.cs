namespace Login.Infrastructure.Model.Catalog;

public class CatalogStage
{
    public int Id { get; set; }
    public int CatalogProcessTypeId { get; set; }
    public CatalogProcessType ProcessType { get; set; } = null!;
    public string Name { get; set; } = "";
    public List<CatalogSubStage> SubStages { get; set; } = [];
}

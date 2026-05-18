namespace Login.Infrastructure.Model.Catalog;

public class CatalogProcessType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<CatalogStage> Stages { get; set; } = [];
}

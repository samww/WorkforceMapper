namespace WorkforceMapper.Server.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal ArrUsd { get; set; }
    public ICollection<ProductApplication> ProductApplications { get; set; } = [];
    public ICollection<ProjectProduct> ProjectProducts { get; set; } = [];
}

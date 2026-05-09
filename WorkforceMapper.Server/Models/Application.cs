namespace WorkforceMapper.Server.Models;

public class Application
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double MinSupportFte { get; set; }
    public double RecommendedSupportFte { get; set; }
    public ICollection<ProductApplication> ProductApplications { get; set; } = [];
    public ICollection<Activity> Activities { get; set; } = [];
    public ICollection<ApplicationDependency> Dependencies { get; set; } = [];
}

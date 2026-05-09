namespace WorkforceMapper.Server.Models;

public class ApplicationDependency
{
    public int ApplicationId { get; set; }
    public int DependsOnApplicationId { get; set; }
    public Application Application { get; set; } = null!;
    public Application DependsOnApplication { get; set; } = null!;
}

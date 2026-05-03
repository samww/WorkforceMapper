namespace WorkforceMapper.Server.Models;

public class ProjectProduct
{
    public int ProjectId { get; set; }
    public int ProductId { get; set; }
    public Project Project { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

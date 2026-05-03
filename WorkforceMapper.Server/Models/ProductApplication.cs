namespace WorkforceMapper.Server.Models;

public class ProductApplication
{
    public int ProductId { get; set; }
    public int ApplicationId { get; set; }
    public Product Product { get; set; } = null!;
    public Application Application { get; set; } = null!;
}

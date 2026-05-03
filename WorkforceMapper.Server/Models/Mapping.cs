namespace WorkforceMapper.Server.Models;

public class Mapping
{
    public int ResourceId { get; set; }
    public int ActivityId { get; set; }
    public string Month { get; set; } = "";
    public decimal Percent { get; set; }
    public Resource Resource { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
}

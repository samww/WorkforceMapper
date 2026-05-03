namespace WorkforceMapper.Server.Models;

public class Activity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public int? ApplicationId { get; set; }
    public int? ProjectId { get; set; }
    public string Division { get; set; } = "";
    public Application? Application { get; set; }
    public Project? Project { get; set; }
    public ICollection<Mapping> Mappings { get; set; } = [];
}

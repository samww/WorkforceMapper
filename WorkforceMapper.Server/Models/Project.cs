namespace WorkforceMapper.Server.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ICollection<ProjectProduct> ProjectProducts { get; set; } = [];
    public ICollection<Activity> Activities { get; set; } = [];
}

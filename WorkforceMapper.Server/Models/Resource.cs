namespace WorkforceMapper.Server.Models;

public class Resource
{
    public int Id { get; set; }
    public string ResourceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public string Company { get; set; } = "";
    public string Seniority { get; set; } = "";
    public string RoleType { get; set; } = "";
    public string Division { get; set; } = "";
    public string Country { get; set; } = "";
    public decimal MonthlyRate { get; set; }
    public int Jan { get; set; }
    public int Feb { get; set; }
    public int Mar { get; set; }
    public int Apr { get; set; }
    public int May { get; set; }
    public int Jun { get; set; }
    public int Jul { get; set; }
    public int Aug { get; set; }
    public int Sep { get; set; }
    public int Oct { get; set; }
    public int Nov { get; set; }
    public int Dec { get; set; }
    public ICollection<Mapping> Mappings { get; set; } = [];

    public bool IsActiveIn(string month) => month[5..] switch
    {
        "01" => Jan == 1,
        "02" => Feb == 1,
        "03" => Mar == 1,
        "04" => Apr == 1,
        "05" => May == 1,
        "06" => Jun == 1,
        "07" => Jul == 1,
        "08" => Aug == 1,
        "09" => Sep == 1,
        "10" => Oct == 1,
        "11" => Nov == 1,
        "12" => Dec == 1,
        _ => false
    };
}

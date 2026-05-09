namespace WorkforceMapper.Server.Data;

public record ResourceSeed(
    string ResourceId, string Name, string ResourceType, string Company,
    string Seniority, string RoleType, string Division, string Country,
    decimal MonthlyRate,
    int Jan, int Feb, int Mar, int Apr, int May,
    int Jun, int Jul, int Aug, int Sep, int Oct, int Nov, int Dec);

public record MappingSeed(string ResourceId, int ActivityId, int Percent, string Month);

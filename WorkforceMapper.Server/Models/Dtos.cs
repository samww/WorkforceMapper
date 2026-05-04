namespace WorkforceMapper.Server.Models;

public record MappingDto(int activity_id, string activity_name, decimal percent, string month);

public record ResourceDto(
    int id,
    string resource_id,
    string name,
    string resource_type,
    string company,
    string seniority,
    string role_type,
    string division,
    string country,
    decimal monthly_rate,
    bool active,
    List<MappingDto> mappings
);

public record ActivityDto(int id, string name, string activity_type, string division, string parent_name);

public record UpsertMappingRequest(int resource_id, int activity_id, string month, decimal percent);

public record ChatRequest(string Query, string? Month);

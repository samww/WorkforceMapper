namespace WorkforceMapper.Server.Models;

public record MappingDto(int activity_id, string activity_name, int percent, string month);

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

public record UpsertMappingRequest(int resource_id, int activity_id, string month, int percent);

public record ChatRequest(string Query, string? Month);

public record ProductRefDto(int id, string name, decimal arr_usd, string? risk = null);

public record ContributingAppDto(int id, string name, string risk, string division);

public record DirectAppNodeDto(int id, string name, string risk, List<ContributingAppDto> dependencies);

public record ProductRiskDto(
    int id,
    string name,
    decimal arr,
    string risk,
    List<ContributingAppDto> contributing_apps,
    List<DirectAppNodeDto> direct_apps
);

public record ApplicationRiskDto(
    int id,
    string name,
    string division,
    double min_support_fte,
    double recommended_support_fte,
    double current_coverage_fte,
    string risk,
    List<ProductRefDto> dependent_products,
    List<ContributingAppDto> transitive_dependent_apps,
    decimal total_dependent_arr_usd,
    int dependent_app_count
);

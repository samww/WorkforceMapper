export interface ResourceMapping {
  activity_id: number;
  activity_name: string;
  percent: number;
}

export interface Resource {
  id: number;
  resource_id: string;
  name: string;
  role_type: string;
  seniority: string;
  resource_type: string;
  country: string;
  division: string;
  monthly_rate: number;
  active: boolean;
  mappings: ResourceMapping[];
}

export interface Activity {
  id: number;
  name: string;
  activity_type: string;
}

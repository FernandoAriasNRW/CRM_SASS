export interface TeamDto {
  id: string;
  name: string;
  description: string;
  memberCount: number;
}

export interface CreateTeamRequest {
  name: string;
  description: string;
  memberIds: string[];
}

export interface UpdateTeamRequest {
  name: string;
  description: string;
  memberIds: string[];
}

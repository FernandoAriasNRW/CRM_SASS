export interface TeamDto {
  id: string;
  name: string;
  description: string;
  memberCount: number;
  /** Los miembros activos. Al editar se vuelven a mandar: la lista sustituye a la que había. */
  memberIds: string[];
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

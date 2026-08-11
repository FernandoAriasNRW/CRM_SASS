import { Injectable, inject } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { TeamDto, CreateTeamRequest, UpdateTeamRequest } from './team.model';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class TeamService {
  private api = inject(ApiService);
  private path = '/teams';

  getTeams(): Observable<TeamDto[]> {
    return this.api.get<TeamDto[]>(this.path);
  }

  getMyTeams(): Observable<TeamDto[]> {
    return this.api.get<TeamDto[]>(`${this.path}/my-teams`);
  }

  getTeamById(id: string): Observable<TeamDto> {
    return this.api.get<TeamDto>(`${this.path}/${id}`);
  }

  createTeam(team: CreateTeamRequest): Observable<string> {
    return this.api.post<string>(this.path, team);
  }

  updateTeam(id: string, team: UpdateTeamRequest): Observable<void> {
    return this.api.put<void>(`${this.path}/${id}`, team);
  }

  deleteTeam(id: string): Observable<void> {
    return this.api.delete<void>(`${this.path}/${id}`);
  }
}

import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideUsers, lucidePlus, lucideEye } from '@ng-icons/lucide';
import { TeamService } from './team.service';
import { TeamDto } from './team.model';
import { ButtonComponent } from '../../shared/ui/button.component';
import { TeamFormComponent } from './team-form.component';

@Component({
  selector: 'app-teams-list',
  standalone: true,
  imports: [CommonModule, RouterModule, NgIconComponent, ButtonComponent, TeamFormComponent],
  viewProviders: [provideIcons({ lucideUsers, lucidePlus, lucideEye })],
  templateUrl: './teams-list.component.html'
})
export class TeamsListComponent implements OnInit {
  private readonly teamService = inject(TeamService);
  private readonly route = inject(ActivatedRoute);

  readonly teams = signal<TeamDto[]>([]);
  readonly isLoading = signal(false);
  readonly isAllTeamsView = signal(false);

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      this.isAllTeamsView.set(params['view'] === 'all');
      this.loadTeams();
    });
  }

  loadTeams() {
    this.isLoading.set(true);
    const request$ = this.isAllTeamsView() 
      ? this.teamService.getTeams() 
      : this.teamService.getMyTeams();

    request$.subscribe({
      next: (res) => {
        this.teams.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  showTeamForm = signal(false);

  openTeamForm() {
    this.showTeamForm.set(true);
  }

  closeTeamForm() {
    this.showTeamForm.set(false);
  }

  onTeamSaved() {
    this.showTeamForm.set(false);
    this.loadTeams();
  }
}

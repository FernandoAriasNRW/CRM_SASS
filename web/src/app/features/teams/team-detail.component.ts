import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideUsers, lucideBriefcase, lucideActivity } from '@ng-icons/lucide';
import { TeamService } from './team.service';
import { TeamDto } from './team.model';
import { ButtonComponent } from '../../shared/ui/button.component';

@Component({
  selector: 'app-team-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, NgIconComponent, ButtonComponent],
  viewProviders: [provideIcons({ lucideArrowLeft, lucideUsers, lucideBriefcase, lucideActivity })],
  templateUrl: './team-detail.component.html'
})
export class TeamDetailComponent implements OnInit {
  private readonly teamService = inject(TeamService);
  private readonly route = inject(ActivatedRoute);

  readonly team = signal<TeamDto | null>(null);
  readonly isLoading = signal(false);

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.loadTeam(id);
      }
    });
  }

  loadTeam(id: string) {
    this.isLoading.set(true);
    this.teamService.getTeamById(id).subscribe({
      next: (res) => {
        this.team.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}

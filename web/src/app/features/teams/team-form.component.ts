import { Component, EventEmitter, inject, Input, Output, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TeamService } from './team.service';
import { CreateTeamRequest, UpdateTeamRequest, TeamDto } from './team.model';
import { ButtonComponent } from '../../shared/ui/button.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideX } from '@ng-icons/lucide';

@Component({
  selector: 'app-team-form',
  standalone: true,
  imports: [CommonModule, FormsModule, DrawerComponent],
  viewProviders: [provideIcons({ lucideX })],
  templateUrl: './team-form.component.html'
})
export class TeamFormComponent implements OnInit {
  @Input() team: TeamDto | null = null;
  @Input() isOpen = false;
  @Output() close = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  private readonly teamService = inject(TeamService);
  
  formData = {
    name: '',
    description: '',
    memberIds: [] as string[]
  };

  isSaving = false;

  ngOnInit() {
    if (this.team) {
      this.formData = {
        name: this.team.name,
        description: this.team.description,
        memberIds: [] // Assuming we fetch or populate memberIds somehow, for now empty
      };
    }
  }

  save() {
    if (!this.formData.name) return;

    this.isSaving = true;
    
    if (this.team) {
      const req: UpdateTeamRequest = this.formData;
      this.teamService.updateTeam(this.team.id, req).subscribe({
        next: () => {
          this.isSaving = false;
          this.saved.emit();
        },
        error: () => this.isSaving = false
      });
    } else {
      const req: CreateTeamRequest = this.formData;
      this.teamService.createTeam(req).subscribe({
        next: () => {
          this.isSaving = false;
          this.saved.emit();
        },
        error: () => this.isSaving = false
      });
    }
  }
}

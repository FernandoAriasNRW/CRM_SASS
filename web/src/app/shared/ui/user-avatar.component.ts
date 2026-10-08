import { Component, Input, computed, inject, OnInit } from '@angular/core';

import { UsersService } from '../../core/users.service';

@Component({
  selector: 'app-user-avatar',
  standalone: true,
  imports: [],
  template: `
    @if (user(); as u) {
      <div class="flex items-center gap-2">
        <!-- Sólo el avatar: el nombre va en el title y en el nombre accesible, que es lo que
             dice de quién son unas iniciales. -->
        <div class="w-8 h-8 rounded-full overflow-hidden flex items-center justify-center text-xs font-medium text-white shadow-sm"
          [style.backgroundColor]="u.avatarUrl ? 'transparent' : getColor(u.id)"
          [title]="u.name" role="img" [attr.aria-label]="u.name">
          @if (u.avatarUrl) {
            <img [src]="u.avatarUrl" [alt]="u.name" class="w-full h-full object-cover" />
          }
          @if (!u.avatarUrl) {
            <span>{{ getInitials(u.name) }}</span>
          }
        </div>
      </div>
    } @else {
      <div class="flex items-center gap-2 animate-pulse">
        <div class="w-8 h-8 rounded-full bg-secondary"></div>
      </div>
    }
    `
})
export class UserAvatarComponent implements OnInit {
  @Input({ required: true }) userId!: string;

  private usersService = inject(UsersService);

  public user = computed(() => this.usersService.getUser(this.userId));

  ngOnInit() {
    // If the cache is empty, trigger a load (usually handled at app initialization, but just in case)
    if (this.usersService.users().length === 0) {
      this.usersService.loadTenantUsers().subscribe();
    }
  }

  getInitials(name: string): string {
    if (!name) return '??';
    const parts = name.split(' ').filter(p => p.length > 0);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return name.substring(0, 2).toUpperCase();
  }

  getColor(id: string): string {
    const colors = [
      '#EF4444', '#F97316', '#F59E0B', '#10B981', '#06B6D4', 
      '#3B82F6', '#6366F1', '#8B5CF6', '#D946EF', '#F43F5E'
    ];
    
    // Simple hash of the UUID
    let hash = 0;
    for (let i = 0; i < id.length; i++) {
      hash = id.charCodeAt(i) + ((hash << 5) - hash);
    }
    
    const index = Math.abs(hash) % colors.length;
    return colors[index];
  }
}

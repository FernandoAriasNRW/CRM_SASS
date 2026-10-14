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
    // Tonos 700: las iniciales van en blanco y los 500 que había no llegaban al 4,5:1 que
    // pide WCAG AA (el naranja se quedaba en 2,8:1). Con estos todos pasan de 5:1.
    const colors = [
      '#B91C1C', '#C2410C', '#B45309', '#047857', '#0E7490',
      '#1D4ED8', '#4338CA', '#6D28D9', '#A21CAF', '#BE123C'
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

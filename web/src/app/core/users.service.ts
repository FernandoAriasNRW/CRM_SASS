import { Injectable, computed, signal } from '@angular/core';
import { ApiService } from './api.service';
import { Observable, tap } from 'rxjs';

export interface TenantUser {
  id: string;
  tenantId: string;
  name: string;
  email: string;
  role: string;
  avatarUrl?: string;
  isActive: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class UsersService {
  private readonly _users = signal<TenantUser[]>([]);
  public readonly users = this._users.asReadonly();
  
  public readonly usersMap = computed(() => {
    const map = new Map<string, TenantUser>();
    for (const user of this._users()) {
      map.set(user.id, user);
    }
    return map;
  });

  constructor(private readonly api: ApiService) {}

  public loadTenantUsers(): Observable<TenantUser[]> {
    return this.api.get<TenantUser[]>('/users/tenant').pipe(
      tap(users => this._users.set(users))
    );
  }

  public getUser(id: string): TenantUser | undefined {
    return this.usersMap().get(id);
  }
}

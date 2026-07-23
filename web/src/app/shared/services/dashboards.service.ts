import { Injectable, inject } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { Observable } from 'rxjs';

export interface Dashboard {
  id: string;
  title: string;
  isDefault: boolean;
  isPublic: boolean;
  widgetsJson: string;
  tagIds: string[];
  createdById: string;
  tenantId: string;
}

@Injectable({ providedIn: 'root' })
export class DashboardsService {
  private readonly api = inject(ApiService);

  getAllDashboards(): Observable<Dashboard[]> {
    return this.api.get<Dashboard[]>('/dashboards');
  }

  getDashboardById(id: string): Observable<Dashboard> {
    return this.api.get<Dashboard>(`/dashboards/${id}`);
  }

  createDashboard(dashboard: Partial<Dashboard>): Observable<Dashboard> {
    return this.api.post<Dashboard>('/dashboards', dashboard);
  }

  updateDashboard(id: string, dashboard: Partial<Dashboard>): Observable<void> {
    return this.api.put<void>(`/dashboards/${id}`, dashboard);
  }

  deleteDashboard(id: string): Observable<void> {
    return this.api.delete<void>(`/dashboards/${id}`);
  }
}

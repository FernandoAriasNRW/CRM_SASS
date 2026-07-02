import { Injectable, inject } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { Observable } from 'rxjs';

export interface SavedView {
  id: string;
  userId: string;
  tenantId: string;
  moduleName: string;
  viewName: string;
  stateJson: string; // JSON containing filters, columns, sorting
  isDefault: boolean;
}

export interface SaveViewPayload {
  moduleName: string;
  viewName: string;
  stateJson: string;
  isDefault: boolean;
}

@Injectable({ providedIn: 'root' })
export class ViewsService {
  private readonly api = inject(ApiService);

  getViews(moduleName: string): Observable<SavedView[]> {
    return this.api.get<SavedView[]>(`/views/${moduleName}`);
  }

  saveView(payload: SaveViewPayload): Observable<SavedView> {
    return this.api.post<SavedView>('/views', payload);
  }

  deleteView(id: string): Observable<void> {
    return this.api.delete<void>(`/views/${id}`);
  }
}

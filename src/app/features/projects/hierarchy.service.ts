import { Injectable, inject } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { Observable } from 'rxjs';

export interface Space {
  id: string;
  name: string;
  description: string;
  color: string;
}

export interface Folder {
  id: string;
  spaceId: string;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class HierarchyService {
  private readonly api = inject(ApiService);

  getSpaces(): Observable<Space[]> {
    return this.api.get<Space[]>('/spaces');
  }

  createSpace(payload: { name: string; description: string; color: string }): Observable<Space> {
    return this.api.post<Space>('/spaces', payload);
  }

  getFolders(spaceId: string): Observable<Folder[]> {
    return this.api.get<Folder[]>('/folders', { spaceId });
  }

  createFolder(payload: { spaceId: string; name: string }): Observable<Folder> {
    return this.api.post<Folder>('/folders', payload);
  }
}

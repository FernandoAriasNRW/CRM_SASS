import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class GuestApiService {
  private readonly baseUrl = 'http://localhost:5239/api/v1';
  private readonly http = inject(HttpClient);

  createTicket(tenantSlug: string, payload: CreateTicketPayload): Observable<unknown> {
    return this.http
      .post<{ accessToken: string }>(`${this.baseUrl}/auth/guest-token`, { tenantSlug })
      .pipe(
        switchMap(({ accessToken }) =>
          this.http.post(`${this.baseUrl}/tickets`, payload, {
            headers: { Authorization: `Bearer ${accessToken}` },
          })
        )
      );
  }
}

export interface CreateTicketPayload {
  subject: string;
  company: string;
  contactName: string;
  contactPhone: string;
  contactEmail: string;
  message: string;
  category: string;
}

import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { provideRouter } from '@angular/router';
import { AuthSignalStore } from './core/auth-signal.store';
import { RealtimeService } from './core/realtime.service';
import { ApiService } from './core/api.service';
import { signal } from '@angular/core';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: AuthSignalStore, useValue: { user: signal(null), isAuthenticated: signal(false) } },
        { provide: RealtimeService, useValue: { connect: () => {} } },
        { provide: ApiService, useValue: {} }
      ]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });
});

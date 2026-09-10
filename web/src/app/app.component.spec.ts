import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { provideRouter } from '@angular/router';
import { AuthSignalStore } from './core/auth-signal.store';
import { RealtimeService } from './core/realtime.service';
import { ApiService } from './core/api.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        // El store de verdad, no un doble: sólo depende de Router, que ya está
        // provisto arriba. El doble que había aquí exponía dos miembros —y uno
        // con el nombre equivocado, `user` en vez de `userInfo`—, así que el
        // efecto de SessionManagerService reventaba con
        // «getTokenExpiresAt is not a function». Angular se traga los errores de
        // un effect: salían por consola y la prueba seguía pasando en verde.
        AuthSignalStore,
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

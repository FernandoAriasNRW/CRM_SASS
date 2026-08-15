import { HttpErrorResponse, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';
import { errorInterceptor } from './error.interceptor';
import { sinAvisoAutomatico } from '../http-context';
import { ToastService } from '../../shared/services/toast.service';
import { AuthSignalStore } from '../auth-signal.store';
import { ApiService } from '../api.service';

/**
 * El interceptor levanta un aviso por cada fallo, y eso está bien para lo que nadie más va a
 * explicar. Pero cuando quien hizo la petición pone el mensaje junto al campo que lo provocó, el
 * del interceptor es el mismo texto repetido: **dos avisos para un solo fallo**.
 */
describe('errorInterceptor — avisos duplicados', () => {
  let toast: jasmine.SpyObj<ToastService>;

  const fallo = (status: number) => new HttpErrorResponse({
    status, error: 'Las horas estimadas no pueden ser negativas',
    url: 'http://localhost:8080/api/v1/tasks/1',
  });

  function interceptar(peticion: HttpRequest<unknown>, status = 400) {
    const siguiente: HttpHandlerFn = () => throwError(() => fallo(status));

    return TestBed.runInInjectionContext(() =>
      errorInterceptor(peticion, siguiente));
  }

  beforeEach(() => {
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['error', 'warning', 'handleHttpError']);

    TestBed.configureTestingModule({
      providers: [
        { provide: ToastService, useValue: toast },
        { provide: AuthSignalStore, useValue: jasmine.createSpyObj('AuthSignalStore', ['logout', 'setAccessToken']) },
        { provide: ApiService, useValue: { post: () => throwError(() => new Error('sin refresco')) } },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) },
      ],
    });
  });

  it('avisa de un fallo que nadie más va a explicar', done => {
    const peticion = new HttpRequest('GET', '/api/v1/tasks');

    interceptar(peticion).subscribe({
      error: () => {
        expect(toast.handleHttpError).toHaveBeenCalled();
        done();
      },
    });
  });

  it('no avisa si quien llamó se reservó explicarlo', done => {
    const peticion = new HttpRequest('GET', '/api/v1/tasks', { context: sinAvisoAutomatico() });

    interceptar(peticion).subscribe({
      error: () => {
        expect(toast.handleHttpError).not.toHaveBeenCalled();
        done();
      },
    });
  });

  /** El error se sigue propagando: quien llamó tiene que poder contarlo. */
  it('el error llega igualmente a quien hizo la petición', done => {
    const peticion = new HttpRequest('GET', '/api/v1/tasks', { context: sinAvisoAutomatico() });

    interceptar(peticion).subscribe({
      error: (e: HttpErrorResponse) => {
        expect(e.status).toBe(400);
        done();
      },
    });
  });
});

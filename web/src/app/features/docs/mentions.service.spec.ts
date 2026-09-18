import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { MentionsService } from './mentions.service';

/**
 * Estas pruebas fijan <b>las rutas exactas</b> que pide el servicio, que es justo lo que se rompió
 * en silencio: la lista de personas se pedía a `/auth/users`, que no existe, y el `catch` del
 * servicio convertía el 404 en «no hay nadie». El desplegable de `@` salía vacío y no había ni un
 * error en ninguna parte.
 *
 * Por eso se comprueba la URL literal y no sólo que «se llamó a la API»: un doble que responde a
 * cualquier ruta habría dejado pasar el fallo tal cual.
 */
describe('MencionesService', () => {
  let service: MentionsService;
  let api: jasmine.SpyObj<ApiService>;

  /** Las rutas que se han pedido, en orden, para poder afirmar sobre ellas. */
  let requested: string[];

  beforeEach(() => {
    requested = [];
    api = jasmine.createSpyObj<ApiService>('ApiService', ['get']);

    api.get.and.callFake((route: string) => {
      requested.push(route);

      if (route.startsWith('/users')) {
        return of([{ id: 'u1', name: 'Ana Ruiz', email: 'ana@ejemplo.com' }]) as any;
      }

      if (route.startsWith('/tasks')) {
        return of({ items: [{ id: 't1', title: 'Migrar la base', status: 'Abierta' }] }) as any;
      }

      return of({ items: [] }) as any;
    });

    TestBed.configureTestingModule({
      providers: [MentionsService, { provide: ApiService, useValue: api }]
    });

    service = TestBed.inject(MentionsService);
  });

  it('busca las personas en /users, pidiendo sólo las que va a enseñar', async () => {
    const candidates = await service.search('@', 'ana');

    expect(requested).toEqual(['/users?pageSize=5&search=ana']);
    expect(candidates).toEqual([
      { id: 'u1', etiqueta: 'Ana Ruiz', tipo: 'Persona', detail: 'ana@ejemplo.com' }
    ]);
  });

  /**
   * El punto de todo el cambio: filtra <b>el servidor</b>. Si se dejara de mandar `search`, la
   * búsqueda volvería a mirar sólo las primeras filas y fallaría únicamente con datos grandes,
   * que es cuando ya no se relaciona con esto.
   */
  it('manda el texto al servidor en las tres listas de #', async () => {
    await service.search('#', 'migrar');

    expect(requested).toEqual([
      '/tasks?pageSize=5&search=migrar',
      '/tickets?pageSize=5&search=migrar',
      '/projects?pageSize=5&search=migrar'
    ]);
  });

  it('escapa lo que se escribe, para que un & no parta la consulta', async () => {
    await service.search('@', 'diseño & obra');

    expect(requested).toEqual(['/users?pageSize=5&search=dise%C3%B1o%20%26%20obra']);
  });

  /** Sin nada escrito no se molesta al servidor: `@` recién tecleado no es una búsqueda. */
  it('no pide nada con la consulta vacía', async () => {
    expect(await service.search('@', '   ')).toEqual([]);
    expect(api.get).not.toHaveBeenCalled();
  });

  /**
   * Un fallo de red no puede reventar el editor, pero <b>tiene que dejar rastro</b>: la ruta mala
   * sobrevivió a un commit entero porque el error no aparecía por ningún lado.
   */
  it('devuelve vacío si la petición falla, pero lo deja en la consola', async () => {
    api.get.and.returnValue(throwError(() => new Error('404')));
    const callout = spyOn(console, 'warn');

    expect(await service.search('@', 'ana')).toEqual([]);
    expect(callout).toHaveBeenCalled();
  });

  it('mezcla tareas, tickets y proyectos en una sola lista', async () => {
    const candidates = await service.search('#', 'migrar');

    expect(candidates).toEqual([
      { id: 't1', etiqueta: 'Migrar la base', tipo: 'Tarea', detail: 'Abierta' }
    ]);
  });
});

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CommandPaletteService } from './command-palette.service';

describe('CommandPaletteService', () => {
  let svc: CommandPaletteService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    svc = TestBed.inject(CommandPaletteService);
    http = TestBed.inject(HttpTestingController);
  });

  it('without a query it offers every section and action', () => {
    expect(svc.results().length).toBeGreaterThan(5);
    expect(svc.results().some(c => c.group === 'Ir a')).toBeTrue();
    expect(svc.results().some(c => c.group === 'Acciones')).toBeTrue();
  });

  it('filters by label', () => {
    svc.query.set('tareas');

    expect(svc.results().length).toBeGreaterThan(0);
    expect(svc.results().every(c => /tarea/i.test(c.label))).toBeTrue();
  });

  it('finds without typing accents', () => {
    // Obligar a teclear el acento exacto rompe el flujo que justifica el paletón.
    svc.query.set('calendario');
    const accented = svc.results().length;

    svc.query.set('CALENDARIO');

    expect(svc.results().length).toBe(accented);
  });

  it('finds by keyword even if it is not in the label', () => {
    svc.query.set('oscuro');

    expect(svc.results().some(c => c.id === 'accion-tema')).toBeTrue();
  });

  it('groups keeping the order of appearance', () => {
    const groups = svc.grouped().map(g => g.name);

    expect(groups[0]).toBe('Ir a');
    expect(new Set(groups).size).toBe(groups.length);
  });

  it('does not call the server with fewer than two characters', () => {
    svc.searchServer('a');

    http.expectNone(() => true);
    expect(svc.searching()).toBeFalse();
  });

  it('searches projects, tasks and tickets at once', () => {
    svc.query.set('crm');
    svc.searchServer('crm');

    for (const route of ['/projects', '/tasks', '/tickets']) {
      const req = http.expectOne(r => r.url.includes(route));
      req.flush({ items: [{ id: '1', name: 'CRM Suite', title: 'CRM Suite' }] });
    }

    expect(svc.results().some(c => c.group === 'Proyectos')).toBeTrue();
    expect(svc.results().some(c => c.group === 'Tareas')).toBeTrue();
    expect(svc.results().some(c => c.group === 'Tickets')).toBeTrue();
    expect(svc.searching()).toBeFalse();
  });

  it('if a module fails, the others still return results', () => {
    svc.query.set('crm');
    svc.searchServer('crm');

    http.expectOne(r => r.url.includes('/projects'))
      .flush({ items: [{ id: '1', name: 'CRM Suite' }] });
    // Un módulo caído no debe vaciar el paletón y hacer creer que no hay nada.
    http.expectOne(r => r.url.includes('/tasks'))
      .flush('boom', { status: 500, statusText: 'Server Error' });
    http.expectOne(r => r.url.includes('/tickets'))
      .flush({ items: [] });

    expect(svc.results().some(c => c.group === 'Proyectos')).toBeTrue();
  });

  it('discards a response that arrives late', () => {
    svc.query.set('crm');
    svc.searchServer('crm');

    // El usuario sigue escribiendo antes de que conteste el servidor.
    svc.query.set('otra cosa');

    for (const route of ['/projects', '/tasks', '/tickets']) {
      http.expectOne(r => r.url.includes(route))
        .flush({ items: [{ id: '1', name: 'CRM Suite', title: 'CRM Suite' }] });
    }

    // Los resultados obsoletos no deben pisar lo que se está escribiendo ahora.
    expect(svc.results().some(c => c.group === 'Proyectos')).toBeFalse();
  });

  it('opening clears the previous query', () => {
    svc.query.set('algo');

    svc.open();

    expect(svc.query()).toBe('');
    expect(svc.isOpen()).toBeTrue();
  });

  afterEach(() => http.verify());
});

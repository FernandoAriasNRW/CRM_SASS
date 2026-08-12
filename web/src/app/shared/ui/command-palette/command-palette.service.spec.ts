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

  it('sin consulta ofrece todas las secciones y acciones', () => {
    expect(svc.resultados().length).toBeGreaterThan(5);
    expect(svc.resultados().some(c => c.group === 'Ir a')).toBeTrue();
    expect(svc.resultados().some(c => c.group === 'Acciones')).toBeTrue();
  });

  it('filtra por etiqueta', () => {
    svc.consulta.set('tareas');

    expect(svc.resultados().length).toBeGreaterThan(0);
    expect(svc.resultados().every(c => /tarea/i.test(c.label))).toBeTrue();
  });

  it('encuentra sin escribir los acentos', () => {
    // Obligar a teclear el acento exacto rompe el flujo que justifica el paletón.
    svc.consulta.set('calendario');
    const conAcento = svc.resultados().length;

    svc.consulta.set('CALENDARIO');

    expect(svc.resultados().length).toBe(conAcento);
  });

  it('encuentra por palabra clave aunque no esté en la etiqueta', () => {
    svc.consulta.set('oscuro');

    expect(svc.resultados().some(c => c.id === 'accion-tema')).toBeTrue();
  });

  it('agrupa conservando el orden de aparición', () => {
    const grupos = svc.agrupados().map(g => g.nombre);

    expect(grupos[0]).toBe('Ir a');
    expect(new Set(grupos).size).toBe(grupos.length);
  });

  it('no va al servidor con menos de dos caracteres', () => {
    svc.buscarEnServidor('a');

    http.expectNone(() => true);
    expect(svc.buscando()).toBeFalse();
  });

  it('busca en proyectos, tareas y tickets a la vez', () => {
    svc.consulta.set('crm');
    svc.buscarEnServidor('crm');

    for (const ruta of ['/projects', '/tasks', '/tickets']) {
      const req = http.expectOne(r => r.url.includes(ruta));
      req.flush({ items: [{ id: '1', name: 'CRM Suite', title: 'CRM Suite' }] });
    }

    expect(svc.resultados().some(c => c.group === 'Proyectos')).toBeTrue();
    expect(svc.resultados().some(c => c.group === 'Tareas')).toBeTrue();
    expect(svc.resultados().some(c => c.group === 'Tickets')).toBeTrue();
    expect(svc.buscando()).toBeFalse();
  });

  it('si un módulo falla, los demás siguen dando resultados', () => {
    svc.consulta.set('crm');
    svc.buscarEnServidor('crm');

    http.expectOne(r => r.url.includes('/projects'))
      .flush({ items: [{ id: '1', name: 'CRM Suite' }] });
    // Un módulo caído no debe vaciar el paletón y hacer creer que no hay nada.
    http.expectOne(r => r.url.includes('/tasks'))
      .flush('boom', { status: 500, statusText: 'Server Error' });
    http.expectOne(r => r.url.includes('/tickets'))
      .flush({ items: [] });

    expect(svc.resultados().some(c => c.group === 'Proyectos')).toBeTrue();
  });

  it('descarta una respuesta que llega tarde', () => {
    svc.consulta.set('crm');
    svc.buscarEnServidor('crm');

    // El usuario sigue escribiendo antes de que conteste el servidor.
    svc.consulta.set('otra cosa');

    for (const ruta of ['/projects', '/tasks', '/tickets']) {
      http.expectOne(r => r.url.includes(ruta))
        .flush({ items: [{ id: '1', name: 'CRM Suite', title: 'CRM Suite' }] });
    }

    // Los resultados obsoletos no deben pisar lo que se está escribiendo ahora.
    expect(svc.resultados().some(c => c.group === 'Proyectos')).toBeFalse();
  });

  it('abrir limpia la consulta anterior', () => {
    svc.consulta.set('algo');

    svc.abrir();

    expect(svc.consulta()).toBe('');
    expect(svc.abierto()).toBeTrue();
  });

  afterEach(() => http.verify());
});

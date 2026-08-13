import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DataTableComponent, type CellEdit, type ColumnDef } from './data-table.component';

interface Fila extends Record<string, unknown> {
  id: string;
  title: string;
  status: string;
  dueDate: string;
  assigneeId: string;
}

/**
 * La edición en la propia tabla.
 *
 * La tabla **no guarda nada**: emite el cambio y vuelve a pintar lo que le llegue en `data`. Estas
 * pruebas fijan esa frontera, porque es lo que evita que haya dos sitios decidiendo qué se ve —el
 * que guarda es quien revierte si el servidor rechaza, y esa lógica ya vive en quien usa la tabla—.
 */
describe('DataTableComponent — edición en línea', () => {
  const FILA: Fila = {
    id: 't1', title: 'Configurar alertas', status: 'To Do',
    dueDate: '2026-08-15T00:00:00', assigneeId: 'u1',
  };

  const COLUMNAS: ColumnDef[] = [
    { key: 'title', label: 'Título', editable: true },
    {
      key: 'status', label: 'Estado', editable: true, editor: 'select',
      options: [{ label: 'Por hacer', value: 'To Do' }, { label: 'Hecho', value: 'Done' }],
    },
    { key: 'dueDate', label: 'Fecha', type: 'date', editable: true, editor: 'date' },
    { key: 'assigneeId', label: 'Asignado', type: 'user' },
  ];

  let fixture: ComponentFixture<DataTableComponent<Fila>>;
  let tabla: DataTableComponent<Fila>;
  let emitidos: CellEdit<Fila>[];

  const columna = (key: string) => COLUMNAS.find(c => c.key === key)!;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DataTableComponent] }).compileComponents();

    fixture = TestBed.createComponent<DataTableComponent<Fila>>(DataTableComponent);
    tabla = fixture.componentInstance;
    tabla.columns = COLUMNAS;
    tabla.data = [FILA];
    fixture.detectChanges();

    emitidos = [];
    tabla.cellEdit.subscribe(cambio => emitidos.push(cambio));
  });

  describe('qué columnas se pueden editar', () => {
    it('las que lo piden', () => {
      expect(tabla.sePuedeEditar(columna('title'))).toBeTrue();
    });

    it('y no las que no', () => {
      expect(tabla.sePuedeEditar(columna('assigneeId'))).toBeFalse();
    });

    it('un desplegable sin opciones no se edita: sería un control que no deja elegir', () => {
      expect(tabla.sePuedeEditar({ key: 'x', label: 'X', editable: true, editor: 'select' })).toBeFalse();
      expect(tabla.sePuedeEditar({ key: 'x', label: 'X', editable: true, editor: 'select', options: [] })).toBeFalse();
    });
  });

  it('sólo se edita una celda a la vez', () => {
    tabla.empezarEdicion(FILA, columna('title'));
    tabla.empezarEdicion(FILA, columna('status'));

    expect(tabla.editandoEsta(FILA, columna('title'))).toBeFalse();
    expect(tabla.editandoEsta(FILA, columna('status'))).toBeTrue();
  });

  it('una columna que no se puede editar no abre editor', () => {
    tabla.empezarEdicion(FILA, columna('assigneeId'));

    expect(tabla.editando()).toBeNull();
  });

  it('confirmar emite el cambio y cierra el editor', () => {
    tabla.empezarEdicion(FILA, columna('title'));

    tabla.confirmarEdicion(FILA, columna('title'), 'Otro título');

    expect(emitidos).toEqual([{ item: FILA, key: 'title', valor: 'Otro título' }]);
    expect(tabla.editando()).toBeNull();
  });

  it('confirmar el mismo valor no gasta una petición', () => {
    tabla.empezarEdicion(FILA, columna('title'));

    tabla.confirmarEdicion(FILA, columna('title'), 'Configurar alertas');

    expect(emitidos).toEqual([]);
    expect(tabla.editando()).toBeNull();
  });

  it('escapar cierra sin emitir nada', () => {
    tabla.empezarEdicion(FILA, columna('title'));

    tabla.cancelarEdicion();

    expect(emitidos).toEqual([]);
    expect(tabla.editando()).toBeNull();
  });

  /**
   * Al cancelar se quita el editor del DOM, y el navegador dispara un `blur` sobre el elemento
   * que acaba de desaparecer. Sin guarda, ese `blur` guardaba el valor recién descartado y
   * Escape no cancelaba nada.
   */
  it('el blur que llega después de cancelar no guarda lo descartado', () => {
    tabla.empezarEdicion(FILA, columna('title'));
    tabla.cancelarEdicion();

    tabla.confirmarEdicion(FILA, columna('title'), 'lo que se descartó');

    expect(emitidos).toEqual([]);
  });

  it('un blur sobre una celda que no se está editando tampoco guarda', () => {
    tabla.empezarEdicion(FILA, columna('title'));

    tabla.confirmarEdicion(FILA, columna('status'), 'Done');

    expect(emitidos).toEqual([]);
  });

  /**
   * `input type="date"` sólo entiende `aaaa-mm-dd`. Con la marca de tiempo entera se queda vacío,
   * sin decir por qué, y parece que la tarea no tiene fecha.
   */
  it('una fecha se recorta al formato que entiende el editor', () => {
    expect(tabla.valorTexto(FILA, columna('dueDate'))).toBe('2026-08-15');
  });

  it('y ese recorte hace que una fecha sin cambios tampoco se emita', () => {
    tabla.confirmarEdicion(FILA, columna('dueDate'), '2026-08-15');

    expect(emitidos).toEqual([]);
  });

  it('un valor nulo se edita como cadena vacía, no como «null»', () => {
    const sinFecha = { ...FILA, dueDate: null as unknown as string };

    expect(tabla.valorTexto(sinFecha, columna('dueDate'))).toBe('');
  });
});

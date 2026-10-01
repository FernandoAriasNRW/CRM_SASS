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
  const ROW: Fila = {
    id: 't1', title: 'Configurar alertas', status: 'To Do',
    dueDate: '2026-08-15T00:00:00', assigneeId: 'u1',
  };

  const COLUMNS: ColumnDef[] = [
    { key: 'title', label: 'Título', editable: true },
    {
      key: 'status', label: 'Estado', editable: true, editor: 'select',
      options: [{ label: 'Por hacer', value: 'To Do' }, { label: 'Hecho', value: 'Done' }],
    },
    { key: 'dueDate', label: 'Fecha', type: 'date', editable: true, editor: 'date' },
    { key: 'assigneeId', label: 'Asignado', type: 'user' },
  ];

  let fixture: ComponentFixture<DataTableComponent<Fila>>;
  let table: DataTableComponent<Fila>;
  let emitted: CellEdit<Fila>[];

  const column = (key: string) => COLUMNS.find(c => c.key === key)!;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DataTableComponent] }).compileComponents();

    fixture = TestBed.createComponent<DataTableComponent<Fila>>(DataTableComponent);
    table = fixture.componentInstance;
    table.columns = COLUMNS;
    table.data = [ROW];
    fixture.detectChanges();

    emitted = [];
    table.cellEdit.subscribe(change => emitted.push(change));
  });

  describe('qué columnas se pueden editar', () => {
    it('las que lo piden', () => {
      expect(table.canEdit(column('title'))).toBeTrue();
    });

    it('y no las que no', () => {
      expect(table.canEdit(column('assigneeId'))).toBeFalse();
    });

    it('un desplegable sin opciones no se edita: sería un control que no deja elegir', () => {
      expect(table.canEdit({ key: 'x', label: 'X', editable: true, editor: 'select' })).toBeFalse();
      expect(table.canEdit({ key: 'x', label: 'X', editable: true, editor: 'select', options: [] })).toBeFalse();
    });
  });

  it('sólo se edita una celda a la vez', () => {
    table.startEdit(ROW, column('title'));
    table.startEdit(ROW, column('status'));

    expect(table.isEditingThis(ROW, column('title'))).toBeFalse();
    expect(table.isEditingThis(ROW, column('status'))).toBeTrue();
  });

  it('una columna que no se puede editar no abre editor', () => {
    table.startEdit(ROW, column('assigneeId'));

    expect(table.editing()).toBeNull();
  });

  it('confirmar emite el cambio y cierra el editor', () => {
    table.startEdit(ROW, column('title'));

    table.confirmEdit(ROW, column('title'), 'Otro título');

    expect(emitted).toEqual([{ item: ROW, key: 'title', value: 'Otro título' }]);
    expect(table.editing()).toBeNull();
  });

  it('confirmar el mismo valor no gasta una petición', () => {
    table.startEdit(ROW, column('title'));

    table.confirmEdit(ROW, column('title'), 'Configurar alertas');

    expect(emitted).toEqual([]);
    expect(table.editing()).toBeNull();
  });

  it('escapar cierra sin emitir nada', () => {
    table.startEdit(ROW, column('title'));

    table.cancelEdit();

    expect(emitted).toEqual([]);
    expect(table.editing()).toBeNull();
  });

  /**
   * Al cancelar se quita el editor del DOM, y el navegador dispara un `blur` sobre el elemento
   * que acaba de desaparecer. Sin guarda, ese `blur` guardaba el valor recién descartado y
   * Escape no cancelaba nada.
   */
  it('el blur que llega después de cancelar no guarda lo descartado', () => {
    table.startEdit(ROW, column('title'));
    table.cancelEdit();

    table.confirmEdit(ROW, column('title'), 'lo que se descartó');

    expect(emitted).toEqual([]);
  });

  it('un blur sobre una celda que no se está editando tampoco guarda', () => {
    table.startEdit(ROW, column('title'));

    table.confirmEdit(ROW, column('status'), 'Done');

    expect(emitted).toEqual([]);
  });

  /**
   * `input type="date"` sólo entiende `aaaa-mm-dd`. Con la marca de tiempo entera se queda vacío,
   * sin decir por qué, y parece que la tarea no tiene fecha.
   */
  it('una fecha se recorta al formato que entiende el editor', () => {
    expect(table.textValue(ROW, column('dueDate'))).toBe('2026-08-15');
  });

  it('y ese recorte hace que una fecha sin cambios tampoco se emita', () => {
    table.confirmEdit(ROW, column('dueDate'), '2026-08-15');

    expect(emitted).toEqual([]);
  });

  it('un valor nulo se edita como cadena vacía, no como «null»', () => {
    const noDate = { ...ROW, dueDate: null as unknown as string };

    expect(table.textValue(noDate, column('dueDate'))).toBe('');
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DataTableComponent, type CellEdit, type ColumnDef } from './data-table.component';

interface Row extends Record<string, unknown> {
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
describe('DataTableComponent — inline editing', () => {
  const ROW: Row = {
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

  let fixture: ComponentFixture<DataTableComponent<Row>>;
  let table: DataTableComponent<Row>;
  let emitted: CellEdit<Row>[];

  const column = (key: string) => COLUMNS.find(c => c.key === key)!;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DataTableComponent] }).compileComponents();

    fixture = TestBed.createComponent<DataTableComponent<Row>>(DataTableComponent);
    table = fixture.componentInstance;
    table.columns = COLUMNS;
    table.data = [ROW];
    fixture.detectChanges();

    emitted = [];
    table.cellEdit.subscribe(change => emitted.push(change));
  });

  describe('which columns can be edited', () => {
    it('those that ask for it', () => {
      expect(table.canEdit(column('title'))).toBeTrue();
    });

    it('and not the others', () => {
      expect(table.canEdit(column('assigneeId'))).toBeFalse();
    });

    it('a dropdown without options is not editable: it would be a control with nothing to choose', () => {
      expect(table.canEdit({ key: 'x', label: 'X', editable: true, editor: 'select' })).toBeFalse();
      expect(table.canEdit({ key: 'x', label: 'X', editable: true, editor: 'select', options: [] })).toBeFalse();
    });
  });

  it('only one cell is edited at a time', () => {
    table.startEdit(ROW, column('title'));
    table.startEdit(ROW, column('status'));

    expect(table.isEditingThis(ROW, column('title'))).toBeFalse();
    expect(table.isEditingThis(ROW, column('status'))).toBeTrue();
  });

  it('a non-editable column opens no editor', () => {
    table.startEdit(ROW, column('assigneeId'));

    expect(table.editing()).toBeNull();
  });

  it('confirming emits the change and closes the editor', () => {
    table.startEdit(ROW, column('title'));

    table.confirmEdit(ROW, column('title'), 'Otro título');

    expect(emitted).toEqual([{ item: ROW, key: 'title', value: 'Otro título' }]);
    expect(table.editing()).toBeNull();
  });

  it('confirming the same value does not spend a request', () => {
    table.startEdit(ROW, column('title'));

    table.confirmEdit(ROW, column('title'), 'Configurar alertas');

    expect(emitted).toEqual([]);
    expect(table.editing()).toBeNull();
  });

  it('escape closes without emitting anything', () => {
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
  it('the blur after cancelling does not save what was discarded', () => {
    table.startEdit(ROW, column('title'));
    table.cancelEdit();

    table.confirmEdit(ROW, column('title'), 'lo que se descartó');

    expect(emitted).toEqual([]);
  });

  it('a blur on a cell not being edited saves nothing either', () => {
    table.startEdit(ROW, column('title'));

    table.confirmEdit(ROW, column('status'), 'Done');

    expect(emitted).toEqual([]);
  });

  /**
   * `input type="date"` sólo entiende `aaaa-mm-dd`. Con la marca de tiempo entera se queda vacío,
   * sin decir por qué, y parece que la tarea no tiene fecha.
   */
  it('a date is trimmed to the format the editor understands', () => {
    expect(table.textValue(ROW, column('dueDate'))).toBe('2026-08-15');
  });

  it('and that trimming means an unchanged date is not emitted either', () => {
    table.confirmEdit(ROW, column('dueDate'), '2026-08-15');

    expect(emitted).toEqual([]);
  });

  it('a null value is edited as an empty string, not as «null»', () => {
    const noDate = { ...ROW, dueDate: null as unknown as string };

    expect(table.textValue(noDate, column('dueDate'))).toBe('');
  });
});

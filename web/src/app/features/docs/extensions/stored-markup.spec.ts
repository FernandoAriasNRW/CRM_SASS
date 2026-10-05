import { Editor, type JSONContent } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import { Details, DetailsContent, DetailsSummary } from '@tiptap/extension-details';
import { Callout } from './callout';
import { Column, Columns } from './columns';
import { CommentMark } from './comment-mark';
import { Mention } from './mention';

/**
 * El HTML que se guarda, leído y vuelto a escribir por el editor.
 *
 * <b>Es el contrato con los datos que ya existen.</b> Las páginas guardan HTML, y la migración
 * `StoredValuesToEnglish` de Docs lo reescribió de `data-mencion-tipo="Tarea"`, `data-tipo="aviso"`,
 * `data-tono="ojo"`… a sus nombres en inglés. Si una extensión leyera otro atributo que el que la
 * migración escribió, al abrir la página los avisos serían párrafos, las columnas bloques seguidos y
 * las menciones texto suelto, y al guardarla se perdería del todo — sin ningún error.
 *
 * Por eso el HTML de aquí es <b>exactamente el que produce la migración</b> (comprobado contra la
 * base de desarrollo), y no el que escribe el editor, que sería comprobarse a sí mismo.
 */
describe('stored page HTML', () => {
  const MIGRATED = [
    '<p><span data-mention-type="Task" data-mention-id="11111111-1111-1111-1111-111111111111" class="mention">#Una tarea</span> ',
    '<span data-mention-type="Person" data-mention-id="22222222-2222-2222-2222-222222222222" class="mention">@Ana</span></p>',
    '<div data-tone="warning" data-type="callout" class="callout"><p>Ojo</p></div>',
    '<div data-tone="success" data-type="callout" class="callout"><p>Bien</p></div>',
    '<div data-count="3" data-type="columns" class="columns">',
    '<div data-type="column" class="column"><p>1</p></div>',
    '<div data-type="column" class="column"><p>2</p></div>',
    '<div data-type="column" class="column"><p>3</p></div></div>',
    '<p>Texto <span data-annotation="66666666-6666-6666-6666-666666666666" class="commented">comentado</span></p>',
    '<details class="toggle"><summary>Título</summary><div data-type="detailsContent"><p>Dentro</p></div></details>'
  ].join('');

  let editor: Editor;

  beforeEach(() => {
    editor = new Editor({
      extensions: [
        StarterKit,
        Details.configure({ persist: true, HTMLAttributes: { class: 'toggle' } }),
        DetailsSummary,
        DetailsContent,
        Callout,
        Columns,
        Column,
        CommentMark,
        Mention
      ],
      content: MIGRATED
    });
  });

  afterEach(() => editor.destroy());

  /** Todos los nodos del documento, aplanados, para buscar por tipo sin recorrer a mano. */
  function nodes(json: JSONContent = editor.getJSON()): JSONContent[] {
    return [json, ...(json.content ?? []).flatMap(child => nodes(child))];
  }

  it('recognises mentions with their type and id', () => {
    const mentions = nodes().filter(n => n.type === 'mention').map(n => n.attrs);

    expect(mentions).toEqual([
      jasmine.objectContaining({ type: 'Task', entityId: '11111111-1111-1111-1111-111111111111' }),
      jasmine.objectContaining({ type: 'Person', entityId: '22222222-2222-2222-2222-222222222222' })
    ]);
  });

  it('recognises callouts with their tone', () => {
    const tones = nodes().filter(n => n.type === 'callout').map(n => n.attrs?.['tone']);

    expect(tones).toEqual(['warning', 'success']);
  });

  it('recognises the three columns', () => {
    const columns = nodes().find(n => n.type === 'columns');

    expect(columns?.attrs?.['count']).toBe(3);
    expect(columns?.content?.filter(c => c.type === 'column').length).toBe(3);
  });

  it('recognises the inline comment with its annotation', () => {
    const marks = nodes().flatMap(n => n.marks ?? []).filter(m => m.type === 'comment');

    expect(marks.map(m => m.attrs?.['annotationId'])).toEqual(['66666666-6666-6666-6666-666666666666']);
  });

  it('recognises the toggle', () => {
    expect(nodes().some(n => n.type === 'details')).toBeTrue();
  });

  it('writes the same attributes back when saving', () => {
    // Lo que el servidor lee (las menciones) y lo que la hoja de estilos pinta (el resto).
    const html = editor.getHTML();

    for (const fragment of [
      'data-mention-type="Task"', 'data-mention-id="11111111-1111-1111-1111-111111111111"',
      'data-mention-type="Person"',
      'data-type="callout"', 'data-tone="warning"', 'data-tone="success"', 'class="callout"',
      'data-type="columns"', 'data-count="3"', 'data-type="column"',
      'data-annotation="66666666-6666-6666-6666-666666666666"', 'class="commented"',
      'class="toggle"'
    ]) {
      expect(html).withContext(fragment).toContain(fragment);
    }
  });
});

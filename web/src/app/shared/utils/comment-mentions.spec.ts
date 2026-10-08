import { commentSegments, mentionLink, toDraft, toStored } from './comment-mentions';

/**
 * El formato de las menciones en comentarios: lo que se ve al escribir, lo que se guarda y lo que
 * se pinta. Es un contrato con el servidor (`CommentMentionReader`); si cambiara de un lado y no
 * del otro, las menciones dejarían de reconocerse sin dar ningún error.
 */
describe('comment mentions', () => {
  const ANA = '11111111-1111-1111-1111-111111111111';
  const TASK = '22222222-2222-2222-2222-222222222222';

  it('stores what was picked, and leaves alone what is just text', () => {
    const stored = toStored('Hola @Ana Pérez, mira #La pasarela y @alguien', [
      { type: 'Person', id: ANA, label: 'Ana Pérez' },
      { type: 'Task', id: TASK, label: 'La pasarela' },
    ]);

    expect(stored).toBe(`Hola @[Ana Pérez](Person:${ANA}), mira @[La pasarela](Task:${TASK}) y @alguien`);
  });

  it('a mention erased from the box is not stored', () => {
    expect(toStored('Ya no menciono a nadie', [{ type: 'Person', id: ANA, label: 'Ana' }]))
      .toBe('Ya no menciono a nadie');
  });

  it('the longer name wins over a shorter one that starts the same', () => {
    const stored = toStored('@Ana Pérez y @Ana', [
      { type: 'Person', id: ANA, label: 'Ana' },
      { type: 'Person', id: TASK, label: 'Ana Pérez' },
    ]);

    expect(stored).toBe(`@[Ana Pérez](Person:${TASK}) y @[Ana](Person:${ANA})`);
  });

  it('editing shows names, and saving gives back the same text', () => {
    const stored = `Para @[Ana](Person:${ANA}) sobre @[La pasarela](Task:${TASK})`;

    const draft = toDraft(stored);

    expect(draft.text).toBe('Para @Ana sobre #La pasarela');
    expect(toStored(draft.text, draft.mentions)).toBe(stored);
  });

  it('splits the stored text into text and mentions to paint them', () => {
    expect(commentSegments(`Hola @[Ana](Person:${ANA})!`)).toEqual([
      { kind: 'text', text: 'Hola ' },
      { kind: 'mention', type: 'Person', id: ANA, label: 'Ana' },
      { kind: 'text', text: '!' },
    ]);
  });

  it('a task links to its detail and a person to nowhere', () => {
    expect(mentionLink('Task', TASK)).toEqual({ route: '/tasks', queryParams: { task: TASK } });
    expect(mentionLink('Person', ANA)).toBeNull();
  });
});

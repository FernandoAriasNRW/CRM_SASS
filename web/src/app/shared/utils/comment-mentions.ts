import type { MentionType } from '../../features/docs/extensions/mention';

/**
 * Las menciones dentro del texto de un comentario.
 *
 * <b>El formato es un contrato con el servidor</b> (`CommentMentionReader`): cada mención se
 * guarda como `@[Nombre](Tipo:id)`. Quien escribe no ve eso, ve `@Ana Pérez` o `#La pasarela`; al
 * enviar se convierte al formato guardado y al editar se vuelve a convertir. Si el formato
 * cambiara aquí y no allí, las menciones dejarían de reconocerse sin dar ningún error.
 */
const TOKEN = /@\[([^\]\r\n]{1,200})\]\((Person|Team|Project|Task|Ticket|Document):([0-9a-fA-F-]{36})\)/g;

/** Una mención ya elegida, con el nombre tal como se ve en el cuadro de texto. */
export interface DraftMention {
  type: MentionType;
  id: string;
  label: string;
}

/** Un trozo del texto ya pintable: texto normal o una mención. */
export type CommentSegment =
  | { kind: 'text'; text: string }
  | { kind: 'mention'; type: MentionType; id: string; label: string };

/** Personas y equipos se escriben con `@`; las cosas con ficha, con `#`. */
export function mentionPrefix(type: MentionType): '@' | '#' {
  return type === 'Person' || type === 'Team' ? '@' : '#';
}

/** El texto guardado, en trozos para pintar cada mención como enlace. */
export function commentSegments(text: string): CommentSegment[] {
  const segments: CommentSegment[] = [];
  let last = 0;

  for (const match of text.matchAll(TOKEN)) {
    const at = match.index ?? 0;
    if (at > last) segments.push({ kind: 'text', text: text.slice(last, at) });
    segments.push({ kind: 'mention', label: match[1], type: match[2] as MentionType, id: match[3] });
    last = at + match[0].length;
  }

  if (last < text.length) segments.push({ kind: 'text', text: text.slice(last) });
  return segments;
}

/** Del texto guardado a lo que se ve en el cuadro, con las menciones que lleva. Para editar. */
export function toDraft(stored: string): { text: string; mentions: DraftMention[] } {
  const mentions: DraftMention[] = [];
  const text = stored.replace(TOKEN, (_all, label: string, type: MentionType, id: string) => {
    mentions.push({ type, id, label });
    return mentionPrefix(type) + label;
  });
  return { text, mentions };
}

/**
 * De lo que se ve en el cuadro al texto que se guarda.
 *
 * Cada mención elegida se busca por cómo se ve —`@Ana Pérez`— y se sustituye por su forma guardada.
 * Si quien escribe la borró o la cambió, ya no está y se queda como texto normal: es lo que se ve.
 */
export function toStored(text: string, mentions: DraftMention[]): string {
  let result = text;
  // Las más largas primero, para que «@Ana» no se coma el principio de «@Ana Pérez».
  for (const m of [...mentions].sort((a, b) => b.label.length - a.label.length)) {
    const visible = mentionPrefix(m.type) + m.label;
    const token = `@[${m.label}](${m.type}:${m.id})`;
    result = result.split(visible).join(token);
  }
  return result;
}

/**
 * A dónde lleva una mención. Una persona no tiene ficha propia en el producto, así que no enlaza.
 */
export function mentionLink(type: string, id: string): { route: string; queryParams: Record<string, string> } | null {
  switch (type) {
    case 'Task': return { route: '/tasks', queryParams: { task: id } };
    case 'Team': return { route: '/tasks', queryParams: { teamId: id, view: 'board' } };
    case 'Project': return { route: '/projects', queryParams: { project: id } };
    case 'Ticket': return { route: '/tickets', queryParams: { ticket: id } };
    case 'Document': return { route: '/docs', queryParams: { doc: id } };
    default: return null;
  }
}

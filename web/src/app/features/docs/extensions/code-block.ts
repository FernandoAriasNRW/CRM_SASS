import CodeBlockLowlight from '@tiptap/extension-code-block-lowlight';

/**
 * Los lenguajes que se ofrecen.
 *
 * Es un recorte de lo que trae `common` de lowlight, no la lista entera: un desplegable con
 * treinta y siete entradas no ayuda a nadie a encontrar la suya. Éstos son los que aparecen en un
 * producto de este tipo; el resto se sigue coloreando si el bloque ya venía con su lenguaje
 * puesto, sólo que no se puede elegir desde aquí.
 */
export const LANGUAGES = [
  { value: 'typescript', name: 'TypeScript' },
  { value: 'javascript', name: 'JavaScript' },
  { value: 'csharp', name: 'C#' },
  { value: 'sql', name: 'SQL' },
  { value: 'json', name: 'JSON' },
  { value: 'xml', name: 'HTML / XML' },
  { value: 'css', name: 'CSS' },
  { value: 'bash', name: 'Shell' },
  { value: 'yaml', name: 'YAML' },
  { value: 'python', name: 'Python' },
  { value: 'java', name: 'Java' },
  { value: 'markdown', name: 'Markdown' }
] as const;

/**
 * El bloque de código, con su lenguaje elegible.
 *
 * <b>Sin el desplegable, lowlight adivina.</b> Y adivinando sobre una línea suelta se equivoca casi
 * siempre: `const total = items.filter(...)` acababa marcado como si `total` fuera un atributo y el
 * punto y coma un comentario. Un bloque de código mal coloreado se lee peor que uno sin colorear.
 *
 * La vista se construye con DOM a mano, que es lo que espera ProseMirror. `contentDOM` apunta al
 * `<code>`: es lo que le dice a ProseMirror dónde va el texto editable, y sin eso el desplegable
 * formaría parte del contenido y se podría borrar escribiendo.
 */
export const CodeBlock = CodeBlockLowlight.extend({
  addNodeView() {
    return ({ node, editor, getPos }) => {
      const container = document.createElement('div');
      container.className = 'bloque-de-codigo';

      const selector = document.createElement('select');
      selector.className = 'bloque-de-codigo__lenguaje';
      selector.setAttribute('aria-label', $localize`Lenguaje del bloque de código`);
      selector.contentEditable = 'false';

      const auto = document.createElement('option');
      auto.value = '';
      auto.textContent = $localize`Automático`;
      selector.appendChild(auto);

      for (const language of LANGUAGES) {
        const option = document.createElement('option');
        option.value = language.value;
        option.textContent = language.name;
        selector.appendChild(option);
      }

      selector.value = node.attrs['language'] ?? '';

      selector.addEventListener('change', () => {
        if (!editor.isEditable) return;

        const position = typeof getPos === 'function' ? getPos() : null;
        if (position === null || position === undefined) return;

        // El cursor entra en el bloque **antes** de cambiar el atributo, y no es por comodidad.
        //
        // El plugin de coloreado sólo recalcula si la transacción añade o quita bloques, si algún
        // paso engloba un bloque entero, o **si la selección está dentro de uno**. Cambiar sólo un
        // atributo no cumple ninguna de las dos primeras: sin esto, se elige «TypeScript» y el
        // código se queda coloreado como estaba hasta que alguien escribe una letra.
        editor.chain()
          .focus()
          .setTextSelection(position + 1)
          .command(({ tr }) => {
            tr.setNodeAttribute(position, 'language', selector.value || null);
            return true;
          })
          .run();
      });

      // Se para aquí: sin esto, abrir el desplegable mueve el cursor del editor y al elegir un
      // lenguaje el foco vuelve a un sitio distinto del que estaba.
      selector.addEventListener('mousedown', (e) => e.stopPropagation());

      const pre = document.createElement('pre');
      const code = document.createElement('code');
      pre.appendChild(code);

      container.appendChild(selector);
      container.appendChild(pre);

      return {
        dom: container,
        contentDOM: code,

        update: (updated) => {
          if (updated.type.name !== node.type.name) return false;
          selector.value = updated.attrs['language'] ?? '';
          return true;
        },

        // El desplegable no es contenido: sin esto, ProseMirror intenta interpretar sus cambios
        // como ediciones del documento.
        ignoreMutation: (mutacion) =>
          mutacion.target === selector || selector.contains(mutacion.target as Node)
      };
    };
  }
});

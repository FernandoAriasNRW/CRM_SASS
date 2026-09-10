import CodeBlockLowlight from '@tiptap/extension-code-block-lowlight';

/**
 * Los lenguajes que se ofrecen.
 *
 * Es un recorte de lo que trae `common` de lowlight, no la lista entera: un desplegable con
 * treinta y siete entradas no ayuda a nadie a encontrar la suya. Éstos son los que aparecen en un
 * producto de este tipo; el resto se sigue coloreando si el bloque ya venía con su lenguaje
 * puesto, sólo que no se puede elegir desde aquí.
 */
export const LENGUAJES = [
  { valor: 'typescript', nombre: 'TypeScript' },
  { valor: 'javascript', nombre: 'JavaScript' },
  { valor: 'csharp', nombre: 'C#' },
  { valor: 'sql', nombre: 'SQL' },
  { valor: 'json', nombre: 'JSON' },
  { valor: 'xml', nombre: 'HTML / XML' },
  { valor: 'css', nombre: 'CSS' },
  { valor: 'bash', nombre: 'Shell' },
  { valor: 'yaml', nombre: 'YAML' },
  { valor: 'python', nombre: 'Python' },
  { valor: 'java', nombre: 'Java' },
  { valor: 'markdown', nombre: 'Markdown' }
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
export const BloqueDeCodigo = CodeBlockLowlight.extend({
  addNodeView() {
    return ({ node, editor, getPos }) => {
      const contenedor = document.createElement('div');
      contenedor.className = 'bloque-de-codigo';

      const selector = document.createElement('select');
      selector.className = 'bloque-de-codigo__lenguaje';
      selector.setAttribute('aria-label', $localize`Lenguaje del bloque de código`);
      selector.contentEditable = 'false';

      const automatico = document.createElement('option');
      automatico.value = '';
      automatico.textContent = $localize`Automático`;
      selector.appendChild(automatico);

      for (const lenguaje of LENGUAJES) {
        const opcion = document.createElement('option');
        opcion.value = lenguaje.valor;
        opcion.textContent = lenguaje.nombre;
        selector.appendChild(opcion);
      }

      selector.value = node.attrs['language'] ?? '';

      selector.addEventListener('change', () => {
        if (!editor.isEditable) return;

        const posicion = typeof getPos === 'function' ? getPos() : null;
        if (posicion === null || posicion === undefined) return;

        // El cursor entra en el bloque **antes** de cambiar el atributo, y no es por comodidad.
        //
        // El plugin de coloreado sólo recalcula si la transacción añade o quita bloques, si algún
        // paso engloba un bloque entero, o **si la selección está dentro de uno**. Cambiar sólo un
        // atributo no cumple ninguna de las dos primeras: sin esto, se elige «TypeScript» y el
        // código se queda coloreado como estaba hasta que alguien escribe una letra.
        editor.chain()
          .focus()
          .setTextSelection(posicion + 1)
          .command(({ tr }) => {
            tr.setNodeAttribute(posicion, 'language', selector.value || null);
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

      contenedor.appendChild(selector);
      contenedor.appendChild(pre);

      return {
        dom: contenedor,
        contentDOM: code,

        update: (nuevo) => {
          if (nuevo.type.name !== node.type.name) return false;
          selector.value = nuevo.attrs['language'] ?? '';
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

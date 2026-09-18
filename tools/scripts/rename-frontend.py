# -*- coding: utf-8 -*-
"""
Renombra identificadores en TypeScript y en plantillas de Angular, sin tocar comentarios ni
textos de interfaz.

    python tools/scripts/rename-frontend.py <mapa.tsv> <ruta> [<ruta>...] [--dry-run]

El mapa lleva dos columnas separadas por tabulador: nombre viejo, nombre nuevo. Las líneas
vacías y las que empiezan por # se ignoran.

Para qué: en el backend el renombrado lo hace Roslyn, que entiende el código. En el frontend no
hay nada equivalente, y un buscar-y-reemplazar corriente entra en los comentarios (que en este
repositorio siguen en español a propósito) y en los textos que ve el usuario, que son el origen
del catálogo i18n: tocarlos cambia los mensajes y rompe las traducciones.

Qué se considera código, y por tanto renombrable:

- en `.ts`, todo menos los comentarios y el contenido de las cadenas; dentro de una plantilla
  con acentos graves, sólo las interpolaciones `${...}`;
- en `.html`, sólo las interpolaciones `{{...}}`, los valores de los enlaces (`[x]="..."`,
  `(x)="..."`, `*x="..."`), las cabeceras de `@if` / `@for` / `@switch` / `@case` y las
  referencias `#nombre`.

Los `data-testid` **no** se tocan: son parte del contrato de las pruebas e2e, y renombrarlos
aquí dejaría las pruebas buscando algo que no existe sin que nada avise. Se cambian a mano, con
su prueba.
"""
import io
import re
import subprocess
import sys

IDENTIFIER = re.compile(r'[A-Za-z_$][\w$]*')


def load_map(path):
    pairs = {}
    for line in io.open(path, encoding='utf-8'):
        line = line.rstrip('\n')
        if not line.strip() or line.lstrip().startswith('#'):
            continue
        old, new = line.split('\t')[:2]
        pairs[old] = new
    return pairs


def replace_identifiers(text, pairs):
    """Sustituye los identificadores del mapa, y sólo si son el token entero."""
    return IDENTIFIER.sub(lambda m: pairs.get(m.group(0), m.group(0)), text)


def rename_typescript(text, pairs):
    """Recorre el fichero separando código de comentarios y cadenas."""
    out = []
    i = 0
    n = len(text)
    code_start = 0

    def flush_code(end):
        out.append(replace_identifiers(text[code_start:end], pairs))

    while i < n:
        two = text[i:i + 2]

        if two == '//':
            flush_code(i)
            end = text.find('\n', i)
            end = n if end < 0 else end
            out.append(text[i:end])
            i = code_start = end
            continue

        if two == '/*':
            flush_code(i)
            end = text.find('*/', i + 2)
            end = n if end < 0 else end + 2
            out.append(text[i:end])
            i = code_start = end
            continue

        char = text[i]

        if char in '\'"':
            flush_code(i)
            end = i + 1
            while end < n and text[end] != char:
                end += 2 if text[end] == '\\' else 1
            end = min(end + 1, n)
            out.append(text[i:end])
            i = code_start = end
            continue

        if char == '`':
            flush_code(i)

            # La plantilla en línea de un componente (`template:` seguido de acento grave) es HTML
            # de Angular, no texto: sus enlaces e interpolaciones nombran miembros de la clase.
            # Tratada como texto, la clase se renombraba y su plantilla no, y dejaba de compilar.
            if re.search(r'\btemplate\s*:\s*$', text[max(0, i - 40):i]):
                end = i + 1
                while end < n and text[end] != '`':
                    end += 2 if text[end] == '\\' else 1
                out.append('`' + rename_template(text[i + 1:end], pairs) + '`')
                i = code_start = min(end + 1, n)
                continue

            # En una plantilla sólo es código lo que va dentro de ${...}: el resto es texto.
            end = i + 1
            partes = ['`']
            while end < n and text[end] != '`':
                if text[end] == '\\':
                    partes.append(text[end:end + 2])
                    end += 2
                    continue
                if text[end:end + 2] == '${':
                    cierre = find_closing_brace(text, end + 1)
                    partes.append('${' + replace_identifiers(text[end + 2:cierre], pairs) + '}')
                    end = cierre + 1
                    continue
                partes.append(text[end])
                end += 1
            partes.append('`')
            out.append(''.join(partes))
            i = code_start = min(end + 1, n)
            continue

        # Una expresión regular literal: no se toca, y hay que saltarla para no confundir su
        # barra con una división ni sus comillas con una cadena.
        if char == '/' and is_regex_start(text, i):
            flush_code(i)
            end = i + 1
            in_class = False
            while end < n:
                if text[end] == '\\':
                    end += 2
                    continue
                if text[end] == '[':
                    in_class = True
                elif text[end] == ']':
                    in_class = False
                elif text[end] == '/' and not in_class:
                    break
                end += 1
            while end < n and text[end].isalpha():
                end += 1
            out.append(text[i:min(end + 1, n)])
            i = code_start = min(end + 1, n)
            continue

        i += 1

    flush_code(n)
    return ''.join(out)


def find_closing_brace(text, start):
    """Índice de la llave que cierra la que empieza en `start`."""
    depth = 0
    for i in range(start, len(text)):
        if text[i] == '{':
            depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0:
                return i
    return len(text) - 1


def is_regex_start(text, i):
    """Si la barra de `i` abre una expresión regular y no es una división."""
    j = i - 1
    while j >= 0 and text[j] in ' \t':
        j -= 1
    return j < 0 or text[j] in '(,=:[!&|?{};\n+*%<>~^'


# Los enlaces de una plantilla: [x]="...", [(x)]="...", (x)="..." y *ngIf="...". El `%` es por
# `[style.width.%]`, que antes se escapaba entero.
#
# Un atributo plano, x="...", **no** es un enlace aunque sea de un componente propio: su valor es
# un texto literal. Tratarlo como expresión cambió `subtitle`, `placeholder`, `aria-label` y el
# `message` de un estado vacío («Sin tareas» → «Sin tasks»), que son textos de la interfaz.
BINDING = re.compile(
    r'(\s(?:\[\(?[\w.$%-]+\)?\]|\([\w.$-]+\)|\*[\w-]+)\s*=\s*")([^"]*)(")')

# `[^{}]` y `[^{}<>]` a propósito: con `.` y re.S, un `{{` o un `@if (` sin cerrar —o escritos
# dentro de un comentario, que es lo que pasó de verdad— hacen que la coincidencia se estire
# hasta el siguiente cierre que aparezca y se lleve por delante todo lo que haya en medio,
# etiquetas y texto incluidos. En la cabecera de un bloque sí se admiten `<` y `>` rodeados de
# espacios, que son comparaciones: sin eso, `@if (celda.horas > 0)` se quedaba sin renombrar
# mientras el resto del fichero sí cambiaba, y la plantilla leía un campo que ya no existe.
INTERPOLATION = re.compile(r'(\{\{)([^{}]*?)(\}\})')
CONTROL_FLOW = re.compile(r'(@(?:else\s+if|if|for|switch|case)\s*\()((?:[^{}<>]|\s[<>]=?\s)*?)(\)\s*\{)')
REFERENCE = re.compile(r'(\s#)([\w$]+)')

# `@else if (...)` también es una cabecera: sin contarla, la primera rama de un bloque cambiaba y
# la segunda no.
#
# La expresión de un ICU —`{palabras(), plural, =0 {...}}`— es código aunque vaya dentro de un
# texto con i18n: sin esto el contador seguía llamando al nombre viejo.
ICU = re.compile(r'(\{\s*)([\w$.()!?]+)(\s*,\s*(?:plural|select)\s*,)')

# `let-comentario` declara una variable de plantilla: se renombran su nombre y su valor, que es la
# clave del contexto que le pasa `ngTemplateOutlet`.
LET = re.compile(r'(\slet-)([\w$]+)(?:(=")([\w$]*)("))?')
HTML_COMMENT = re.compile(r'<!--.*?-->', re.S)


def rename_template(text, pairs):
    # Una expresión de plantilla se recorre como TypeScript para respetar sus cadenas: en
    # `[subtitle]="'Detalles del proyecto'"` la cadena es texto de la interfaz, no código.
    def expresion(trozo):
        return rename_typescript(trozo, pairs)

    def en_enlace(m):
        if 'data-testid' in m.group(1):
            return m.group(0)
        return m.group(1) + expresion(m.group(2)) + m.group(3)

    def renombrar(trozo):
        trozo = INTERPOLATION.sub(
            lambda m: m.group(1) + expresion(m.group(2)) + m.group(3), trozo)
        trozo = CONTROL_FLOW.sub(
            lambda m: m.group(1) + expresion(m.group(2)) + m.group(3), trozo)
        trozo = BINDING.sub(en_enlace, trozo)
        trozo = ICU.sub(lambda m: m.group(1) + expresion(m.group(2)) + m.group(3), trozo)
        trozo = LET.sub(lambda m: m.group(1) + pairs.get(m.group(2), m.group(2)) + (
            m.group(3) + pairs.get(m.group(4), m.group(4)) + m.group(5) if m.group(3) else ''), trozo)
        return REFERENCE.sub(lambda m: m.group(1) + pairs.get(m.group(2), m.group(2)), trozo)

    # Los comentarios se apartan antes de tocar nada: son español y explican el por qué. Ya
    # costó una vuelta que un `@if` citado dentro de uno se comiera la etiqueta siguiente.
    partes = []
    fin = 0
    for comentario in HTML_COMMENT.finditer(text):
        partes.append(renombrar(text[fin:comentario.start()]))
        partes.append(comentario.group(0))
        fin = comentario.end()
    partes.append(renombrar(text[fin:]))
    return ''.join(partes)


def main(argv):
    if len(argv) < 2:
        sys.exit('Uso: rename-frontend.py <mapa.tsv> <ruta> [<ruta>...] [--dry-run]')

    dry_run = '--dry-run' in argv
    argv = [a for a in argv if a != '--dry-run']
    pairs = load_map(argv[0])

    listed = subprocess.run(['git', 'ls-files'] + argv[1:], capture_output=True, text=True,
                            encoding='utf-8').stdout
    files = [f for f in listed.split() if f.endswith(('.ts', '.html'))]

    total = 0
    for path in files:
        original = io.open(path, encoding='utf-8').read()
        renamed = (rename_template if path.endswith('.html') else rename_typescript)(original, pairs)
        if renamed == original:
            continue

        changed = sum(1 for a, b in zip(original.split('\n'), renamed.split('\n')) if a != b)
        total += changed
        print('%s  (%d líneas)' % (path, changed))
        if not dry_run:
            io.open(path, 'w', encoding='utf-8', newline='\n').write(renamed)

    print('\n== %d líneas en %d ficheros%s' % (total, len(files), ' (simulado)' if dry_run else ''))


if __name__ == '__main__':
    main(sys.argv[1:])

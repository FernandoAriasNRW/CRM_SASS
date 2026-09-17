# -*- coding: utf-8 -*-
"""
Divide un fichero C# en un fichero por tipo de primer nivel.

    python tools/scripts/split-types.py <fichero.cs> <carpeta destino>
                                        [--namespace Nuevo.Namespace] [--keep-original]

Conserva los `using` de cabecera en cada fichero nuevo y cada tipo se lleva los comentarios XML y
los atributos que tenía justo encima. Con `--namespace` cambia el namespace de ámbito de fichero.
Sin `--keep-original`, borra el fichero de origen.

Para qué: los ficheros `*Cqrs.cs` del proyecto acumulan hasta veinte tipos —comandos, consultas,
handlers, DTO y repositorios juntos—, y el estándar pide un tipo público por fichero. Hacerlo a
mano se lleva comentarios por el camino.

Sólo entiende namespaces de ámbito de fichero (`namespace X;`), que es lo que usa el proyecto.
"""
import io
import os
import re
import subprocess
import sys

DECLARATION = re.compile(
    r'^(?:(?:public|internal|private|protected|file|static|sealed|abstract|partial|readonly|record|ref)\s+)*'
    r'(class|record|interface|enum|struct)\s+(\w+)', re.M)


def end_of_type(text, start):
    """Índice tras el final del tipo que empieza en `start`: su llave de cierre, o el `;` de un record posicional."""
    braces = parens = 0
    i = start
    string_delimiter = None
    while i < len(text):
        char = text[i]
        if string_delimiter:
            if text.startswith('"""', i) and string_delimiter == '"""':
                string_delimiter = None
                i += 3
                continue
            if string_delimiter != '"""':
                if char == '\\' and string_delimiter != '@"':
                    i += 2
                    continue
                if char == string_delimiter[-1]:
                    string_delimiter = None
            i += 1
            continue
        if text.startswith('//', i):
            newline = text.find('\n', i)
            i = len(text) if newline < 0 else newline
            continue
        if text.startswith('/*', i):
            i = text.find('*/', i) + 2
            continue
        if text.startswith('"""', i):
            string_delimiter = '"""'
            i += 3
            continue
        if text.startswith('@"', i) or text.startswith('$"', i):
            string_delimiter = '"'
            i += 2
            continue
        if char in '"\'':
            string_delimiter = char
            i += 1
            continue
        if char == '(':
            parens += 1
        elif char == ')':
            parens -= 1
        elif char == '{':
            braces += 1
        elif char == '}':
            braces -= 1
            if braces == 0 and parens == 0:
                return i + 1
        elif char == ';' and braces == 0 and parens == 0:
            return i + 1
        i += 1
    return len(text)


def start_with_comments(text, declaration_start):
    """Retrocede sobre los comentarios XML y los atributos que preceden a la declaración."""
    lines = text[:declaration_start].split('\n')
    index = len(lines) - 1
    while index - 1 >= 0 and re.match(r'^\s*(///|\[)', lines[index - 1]):
        index -= 1
    return len('\n'.join(lines[:index])) + (1 if index > 0 else 0)


def main(argv):
    if len(argv) < 2:
        sys.exit('Uso: split-types.py <fichero.cs> <carpeta destino> [--namespace X] [--keep-original]')

    source, target_folder = argv[0], argv[1]
    new_namespace = argv[argv.index('--namespace') + 1] if '--namespace' in argv else None
    keep_original = '--keep-original' in argv

    text = io.open(source, encoding='utf-8-sig').read().replace('\r\n', '\n')

    namespace_match = re.search(r'^namespace\s+([\w.]+)\s*;\s*$', text, re.M)
    if not namespace_match:
        sys.exit('Sólo se admiten namespaces de ámbito de fichero: ' + source)

    header = text[:namespace_match.start()]
    usings = [line for line in header.split('\n') if line.strip().startswith(('using ', 'global using '))]
    namespace = new_namespace or namespace_match.group(1)
    body = text[namespace_match.end():]

    types = []
    position = 0
    while True:
        match = DECLARATION.search(body, position)
        if not match:
            break
        # Sólo tipos de primer nivel: los anidados van con su tipo contenedor.
        if body[match.start() - 1:match.start()] not in ('', '\n'):
            position = match.end()
            continue
        start = start_with_comments(body, match.start())
        end = end_of_type(body, match.start())
        types.append((match.group(2), body[start:end].strip('\n')))
        position = end

    if not types:
        sys.exit('No se encontraron tipos en ' + source)

    os.makedirs(target_folder, exist_ok=True)
    written = set()
    for name, block in types:
        path = os.path.join(target_folder, name + '.cs')
        if os.path.exists(path) and os.path.abspath(path) != os.path.abspath(source):
            sys.exit('Ya existe: ' + path)

        parts = []
        if usings:
            parts.append('\n'.join(usings) + '\n')
        parts.append(f'namespace {namespace};\n')
        parts.append(block + '\n')
        io.open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(parts))
        written.add(os.path.abspath(path))
        print('  ' + path)

    if not keep_original and os.path.abspath(source) not in written:
        tracked = subprocess.run(['git', 'ls-files', '--error-unmatch', source], capture_output=True).returncode == 0
        if tracked:
            subprocess.run(['git', 'rm', '-q', '--cached', source])
        os.remove(source)

    print(f'{len(types)} tipos desde {source}')


if __name__ == '__main__':
    main(sys.argv[1:])

# -*- coding: utf-8 -*-
"""
Busca identificadores en español en el código de unas rutas.

    pip install wordfreq
    python tools/scripts/spanish-identifiers.py src/Modules/Docs web/src/app/features/docs

Imprime, por fichero, los identificadores que llevan alguna palabra española, y al final cuántos
son y qué nombres de fichero están en español.

Cómo decide si una palabra es española: compara su frecuencia real en los dos idiomas
(`wordfreq`), en vez de usar una lista fija que siempre se queda corta. Las palabras técnicas que
confunden a esa medida están en NOT_SPANISH; las que la medida no reconoce, en ALWAYS_SPANISH.

No mira comentarios ni textos: los comentarios y la interfaz siguen en español a propósito. En las
plantillas de Angular sólo mira las expresiones, no el texto visible.
"""
import collections
import io
import re
import subprocess
import sys

from wordfreq import zipf_frequency

# Palabras técnicas o inglesas que la frecuencia toma por españolas.
NOT_SPANISH = set('''id ids api url dto db utc json html csv pdf uuid guid http https app ui ux es en min max
no nos dtos jwt hmac sha md base media total motor normal general final natural real ideal local global
simple digital error editor color factor doctor actor sector vector tenant outbox'''.split())

# Palabras españolas que la frecuencia no reconoce (suelen ir sin tilde en el código).
ALWAYS_SPANISH = set('''de del la las el los al con para por sin en un una que como mis tu su es
anadir contrasena tope quitar guardar buscar cuantos marcar alternar'''.split())

IDENTIFIER = re.compile(r'(?<![\w$])[A-Za-z_ÁÉÍÓÚÑáéíóúñ][\wÁÉÍÓÚÑáéíóúñ]*')
SPANISH_SUFFIX = re.compile(r'(cion|ador|adora|dor|miento|idad|oso|osa|ando|iendo|ados|adas|idos|idas)$')

_cache = {}


def is_spanish(word):
    word = word.lower()
    if word in _cache:
        return _cache[word]

    if len(word) <= 1 or word in NOT_SPANISH:
        result = False
    elif word in ALWAYS_SPANISH or re.search('[áéíóúñ]', word):
        result = True
    else:
        english, spanish = zipf_frequency(word, 'en'), zipf_frequency(word, 'es')
        result = (spanish >= 2.3 and spanish - english >= 0.8) or (
            english == 0 and spanish == 0 and SPANISH_SUFFIX.search(word) is not None)

    _cache[word] = result
    return result


def words_of(identifier):
    return re.findall(r'[A-ZÁÉÍÓÚÑ]?[a-záéíóúñü]+|[A-ZÁÉÍÓÚÑ]+(?![a-záéíóúñ])|\d+', identifier)


def code_of(path, text):
    """El texto que contiene identificadores: sin comentarios ni cadenas."""
    if path.endswith('.html'):
        # En una plantilla sólo son código las interpolaciones, los enlaces y los bloques @if/@for.
        chunks = re.findall(r'\{\{(.*?)\}\}', text, re.S)
        chunks += re.findall(r'\s[\(\[\*#]?[\w.\-]+[\)\]]?="([^"]*)"', text)
        chunks += re.findall(r'@(?:if|for|switch|case)\s*\((.*?)\)\s*\{', text, re.S)
        chunks += re.findall(r'<(app-[\w-]+)', text)
        return ' '.join(chunks)

    text = re.sub(r'/\*.*?\*/', ' ', text, flags=re.S)
    text = re.sub(r'(?<!:)//.*', ' ', text)
    text = re.sub(r'@?\$?"""(?:.|\n)*?"""', ' ', text)
    text = re.sub(r'\$?@?"(?:[^"\\\n]|\\.)*"|\'(?:[^\'\\\n]|\\.)*\'', ' ', text)
    # En las plantillas de TypeScript se conservan las expresiones ${...}, que son código.
    return re.sub(r'`((?:[^`\\]|\\.)*)`',
                  lambda m: ' '.join(re.findall(r'\$\{(.*?)\}', m.group(1))), text, flags=re.S)


def main(prefixes):
    if not prefixes:
        sys.exit('Uso: spanish-identifiers.py <ruta> [<ruta>...]')

    listed = subprocess.run(['git', 'ls-files'] + prefixes, capture_output=True, text=True, encoding='utf-8').stdout
    files = [f for f in listed.split() if f.endswith(('.cs', '.ts', '.html')) and '/Migrations/' not in f]

    all_identifiers = collections.Counter()
    by_file = {}
    for path in files:
        text = io.open(path, encoding='utf-8-sig', errors='ignore').read()
        found = sorted({i for i in IDENTIFIER.findall(code_of(path, text))
                        if any(is_spanish(w) for w in words_of(i))})
        if found:
            by_file[path] = found
            all_identifiers.update(found)

    for path, identifiers in by_file.items():
        print(f'{path}  ({len(identifiers)})')
        print('   ' + ', '.join(identifiers))

    spanish_names = [f for f in files
                     if any(is_spanish(w) for w in words_of(
                         re.sub(r'[-._]', ' ', f.split('/')[-1].rsplit('.', 1)[0]).title().replace(' ', '')))]

    print(f'\n== {len(by_file)} ficheros, {len(all_identifiers)} identificadores distintos')
    print(f'== nombres de fichero en español: {len(spanish_names)}')
    for name in spanish_names:
        print('   ' + name)


if __name__ == '__main__':
    main(sys.argv[1:])

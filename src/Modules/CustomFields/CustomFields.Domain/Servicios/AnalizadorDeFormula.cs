using System.Globalization;
using System.Text;

namespace CustomFields.Domain.Servicios;

/// <summary>
/// Convierte el texto de una fórmula en un árbol que se puede evaluar, o dice por qué no.
///
/// Es una función pura y vive en el dominio, como <see cref="ValidadorDeValor"/> y por el mismo
/// motivo: es donde se cuela la basura. Una fórmula mal escrita tiene que fallar **al
/// guardarla**, con un mensaje que diga dónde, y no meses después al abrir una tarea.
///
/// ── Decisiones del lenguaje ────────────────────────────────────────────────────────────────
///
/// **El separador de argumentos es `;` y no `,`.** En español la coma es el separador decimal y
/// quien escriba `REDONDEAR(1,5; 0)` espera un número y medio, no dos argumentos. Elegir la
/// coma obligaría a prohibir la coma decimal, que es justo lo que <see cref="ValidadorDeValor"/>
/// se molesta en aceptar.
///
/// **Las referencias van entre corchetes**: `[Horas estimadas]`. Sin ellas habría que prohibir
/// los espacios en los nombres de campo, y los campos se llaman «Coste por hora», no
/// «coste_por_hora».
///
/// **No hay texto ni concatenación.** Una fórmula devuelve un número. Añadir cadenas trae
/// detrás la comparación de cadenas, las mayúsculas, los acentos y la ordenación, y nada de eso
/// hace falta para lo que se pide: totales, márgenes y semáforos.
/// </summary>
public static class AnalizadorDeFormula
{
    public const int LargoMaximo = 500;

    /// <summary>Cuántos nodos puede tener el árbol. Frena una fórmula escrita para hacer daño.</summary>
    public const int MaximoDeNodos = 200;

    // ── El árbol ───────────────────────────────────────────────────────────────────────────

    public abstract record Nodo;

    public sealed record Constante(decimal Valor) : Nodo;

    /// <summary>Una referencia a otro campo, por su nombre tal como se escribió.</summary>
    public sealed record Referencia(string Nombre) : Nodo;

    public sealed record Operacion(string Operador, Nodo Izquierda, Nodo Derecha) : Nodo;

    public sealed record Negacion(Nodo Interior) : Nodo;

    public sealed record Llamada(string Funcion, IReadOnlyList<Nodo> Argumentos) : Nodo;

    public sealed record Resultado(bool EsValida, Nodo? Arbol, string? Error)
    {
        public static Resultado Bien(Nodo arbol) => new(true, arbol, null);
        public static Resultado Mal(string error) => new(false, null, error);
    }

    /// <summary>Las funciones que existen, con cuántos argumentos aceptan.</summary>
    public static class Funciones
    {
        public const string Si = "SI";
        public const string Redondear = "REDONDEAR";
        public const string Minimo = "MIN";
        public const string Maximo = "MAX";
        public const string Absoluto = "ABS";

        public static IReadOnlyList<string> Todas() => [Si, Redondear, Minimo, Maximo, Absoluto];

        /// <summary>
        /// Cuántos argumentos admite. `null` en el segundo elemento significa «sin tope»: MIN y
        /// MAX aceptan los que sean, que es como se usan.
        /// </summary>
        public static (int Minimo, int? Maximo)? Aridad(string funcion) => funcion switch
        {
            Si => (3, 3),
            Redondear => (2, 2),
            Minimo or Maximo => (2, null),
            Absoluto => (1, 1),
            _ => null,
        };
    }

    // ── Entrada pública ────────────────────────────────────────────────────────────────────

    public static Resultado Analizar(string? formula)
    {
        var texto = (formula ?? string.Empty).Trim();

        if (texto.Length == 0)
            return Resultado.Mal(Errores.Vacia);

        if (texto.Length > LargoMaximo)
            return Resultado.Mal(string.Format(Errores.DemasiadoLarga, LargoMaximo));

        var trozos = Trocear(texto, out var errorLexico);
        if (errorLexico is not null)
            return Resultado.Mal(errorLexico);

        var lector = new Lector(trozos!);

        Nodo arbol;
        try
        {
            arbol = LeerComparacion(lector);
        }
        catch (FormulaInvalidaException ex)
        {
            return Resultado.Mal(ex.Message);
        }

        // Si sobra algo después de la expresión, la fórmula está mal aunque el principio se
        // haya podido leer: `2 + 3 )` no es «2 + 3».
        if (!lector.SeAcabo)
            return Resultado.Mal(string.Format(Errores.SobraTexto, lector.Actual!.Texto));

        return ContarNodos(arbol) > MaximoDeNodos
            ? Resultado.Mal(string.Format(Errores.DemasiadoCompleja, MaximoDeNodos))
            : Resultado.Bien(arbol);
    }

    /// <summary>
    /// Los campos a los que apunta una fórmula, sin repetir.
    ///
    /// Lo usa la detección de ciclos y la de referencias a campos que no existen. Se saca del
    /// árbol y no con una expresión regular sobre el texto: un corchete dentro de lo que sea
    /// daría una referencia fantasma.
    /// </summary>
    public static IReadOnlyList<string> ReferenciasDe(Nodo arbol)
    {
        var encontradas = new List<string>();
        Recorrer(arbol, n =>
        {
            if (n is Referencia r && !encontradas.Contains(r.Nombre, StringComparer.OrdinalIgnoreCase))
                encontradas.Add(r.Nombre);
        });
        return encontradas;
    }

    public static void Recorrer(Nodo nodo, Action<Nodo> visitar)
    {
        visitar(nodo);

        switch (nodo)
        {
            case Operacion o:
                Recorrer(o.Izquierda, visitar);
                Recorrer(o.Derecha, visitar);
                break;
            case Negacion n:
                Recorrer(n.Interior, visitar);
                break;
            case Llamada l:
                foreach (var a in l.Argumentos) Recorrer(a, visitar);
                break;
        }
    }

    private static int ContarNodos(Nodo arbol)
    {
        var cuantos = 0;
        Recorrer(arbol, _ => cuantos++);
        return cuantos;
    }

    // ── Análisis léxico ────────────────────────────────────────────────────────────────────

    private enum Clase { Numero, Referencia, Identificador, Operador, AbrePar, CierraPar, Separador }

    private sealed record Trozo(Clase Clase, string Texto, int Posicion);

    private static List<Trozo>? Trocear(string texto, out string? error)
    {
        error = null;
        var trozos = new List<Trozo>();
        var i = 0;

        while (i < texto.Length)
        {
            var c = texto[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || c == '.' || c == ',')
            {
                var inicio = i;
                var conDecimal = false;

                while (i < texto.Length && (char.IsDigit(texto[i]) || texto[i] == '.' || texto[i] == ','))
                {
                    if (texto[i] is '.' or ',')
                    {
                        // Dos separadores decimales en el mismo número es un error de quien
                        // escribe, no un número raro. Detectarlo aquí da un mensaje concreto;
                        // dejarlo pasar daría un fallo de conversión sin posición.
                        if (conDecimal) { error = string.Format(Errores.NumeroMalEscrito, texto[inicio..(i + 1)]); return null; }
                        conDecimal = true;
                    }
                    i++;
                }

                trozos.Add(new Trozo(Clase.Numero, texto[inicio..i], inicio));
                continue;
            }

            if (c == '[')
            {
                var cierre = texto.IndexOf(']', i + 1);
                if (cierre < 0) { error = Errores.CorcheteSinCerrar; return null; }

                var nombre = texto[(i + 1)..cierre].Trim();
                if (nombre.Length == 0) { error = Errores.ReferenciaVacia; return null; }

                trozos.Add(new Trozo(Clase.Referencia, nombre, i));
                i = cierre + 1;
                continue;
            }

            if (char.IsLetter(c))
            {
                var inicio = i;
                while (i < texto.Length && (char.IsLetterOrDigit(texto[i]) || texto[i] == '_')) i++;
                trozos.Add(new Trozo(Clase.Identificador, texto[inicio..i].ToUpperInvariant(), inicio));
                continue;
            }

            // Los de dos caracteres primero: si no, `<=` se leería como `<` seguido de `=`.
            if (i + 1 < texto.Length)
            {
                var dos = texto.Substring(i, 2);
                if (dos is "<=" or ">=" or "<>")
                {
                    trozos.Add(new Trozo(Clase.Operador, dos, i));
                    i += 2;
                    continue;
                }
            }

            switch (c)
            {
                case '+' or '-' or '*' or '/' or '<' or '>' or '=':
                    trozos.Add(new Trozo(Clase.Operador, c.ToString(), i)); i++; continue;
                case '(':
                    trozos.Add(new Trozo(Clase.AbrePar, "(", i)); i++; continue;
                case ')':
                    trozos.Add(new Trozo(Clase.CierraPar, ")", i)); i++; continue;
                case ';':
                    trozos.Add(new Trozo(Clase.Separador, ";", i)); i++; continue;
                default:
                    error = string.Format(Errores.CaracterInesperado, c, i + 1);
                    return null;
            }
        }

        return trozos.Count == 0 ? null : trozos;
    }

    // ── Análisis sintáctico, descendente recursivo ─────────────────────────────────────────

    private sealed class Lector(List<Trozo> trozos)
    {
        private int _i;

        public bool SeAcabo => _i >= trozos.Count;
        public Trozo? Actual => _i < trozos.Count ? trozos[_i] : null;

        public Trozo Consumir() => trozos[_i++];

        public bool SiguienteEs(Clase clase, params string[] textos) =>
            Actual is { } t && t.Clase == clase && (textos.Length == 0 || textos.Contains(t.Texto));
    }

    private sealed class FormulaInvalidaException(string mensaje) : Exception(mensaje);

    /// <summary>
    /// Comparación. Va arriba del todo porque es la de menor precedencia y **no encadena**:
    /// `1 &lt; 2 &lt; 3` es un error, no una cadena que se evalúa por pares como en algunos
    /// lenguajes. Encadenarla sin darse cuenta produce resultados que parecen correctos.
    /// </summary>
    private static Nodo LeerComparacion(Lector lector)
    {
        var izquierda = LeerSuma(lector);

        if (!lector.SiguienteEs(Clase.Operador, "=", "<>", "<", "<=", ">", ">="))
            return izquierda;

        var operador = lector.Consumir().Texto;
        var derecha = LeerSuma(lector);

        if (lector.SiguienteEs(Clase.Operador, "=", "<>", "<", "<=", ">", ">="))
            throw new FormulaInvalidaException(Errores.ComparacionEncadenada);

        return new Operacion(operador, izquierda, derecha);
    }

    private static Nodo LeerSuma(Lector lector)
    {
        var nodo = LeerProducto(lector);

        while (lector.SiguienteEs(Clase.Operador, "+", "-"))
        {
            var operador = lector.Consumir().Texto;
            nodo = new Operacion(operador, nodo, LeerProducto(lector));
        }

        return nodo;
    }

    private static Nodo LeerProducto(Lector lector)
    {
        var nodo = LeerUnario(lector);

        while (lector.SiguienteEs(Clase.Operador, "*", "/"))
        {
            var operador = lector.Consumir().Texto;
            nodo = new Operacion(operador, nodo, LeerUnario(lector));
        }

        return nodo;
    }

    private static Nodo LeerUnario(Lector lector)
    {
        if (!lector.SiguienteEs(Clase.Operador, "-"))
            return LeerPrimario(lector);

        lector.Consumir();
        return new Negacion(LeerUnario(lector));
    }

    private static Nodo LeerPrimario(Lector lector)
    {
        if (lector.SeAcabo)
            throw new FormulaInvalidaException(Errores.TerminaAntesDeTiempo);

        var trozo = lector.Consumir();

        switch (trozo.Clase)
        {
            case Clase.Numero:
                // La coma decimal se admite al escribir y se lee como punto, igual que en
                // ValidadorDeValor: quien escribe en español pone «1,5».
                var normalizado = trozo.Texto.Replace(',', '.');
                return decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero)
                    ? new Constante(numero)
                    : throw new FormulaInvalidaException(string.Format(Errores.NumeroMalEscrito, trozo.Texto));

            case Clase.Referencia:
                return new Referencia(trozo.Texto);

            case Clase.AbrePar:
                var dentro = LeerComparacion(lector);
                if (!lector.SiguienteEs(Clase.CierraPar))
                    throw new FormulaInvalidaException(Errores.ParentesisSinCerrar);
                lector.Consumir();
                return dentro;

            case Clase.Identificador:
                return LeerLlamada(lector, trozo);

            default:
                throw new FormulaInvalidaException(string.Format(Errores.SeEsperabaValor, trozo.Texto));
        }
    }

    private static Nodo LeerLlamada(Lector lector, Trozo nombre)
    {
        var aridad = Funciones.Aridad(nombre.Texto);
        if (aridad is null)
            throw new FormulaInvalidaException(string.Format(
                Errores.FuncionDesconocida, nombre.Texto, string.Join(", ", Funciones.Todas())));

        if (!lector.SiguienteEs(Clase.AbrePar))
            throw new FormulaInvalidaException(string.Format(Errores.FaltaParentesis, nombre.Texto));
        lector.Consumir();

        var argumentos = new List<Nodo>();

        if (!lector.SiguienteEs(Clase.CierraPar))
        {
            argumentos.Add(LeerComparacion(lector));

            while (lector.SiguienteEs(Clase.Separador))
            {
                lector.Consumir();
                argumentos.Add(LeerComparacion(lector));
            }
        }

        if (!lector.SiguienteEs(Clase.CierraPar))
            throw new FormulaInvalidaException(Errores.ParentesisSinCerrar);
        lector.Consumir();

        var (minimo, maximo) = aridad.Value;

        if (argumentos.Count < minimo || (maximo is not null && argumentos.Count > maximo))
            throw new FormulaInvalidaException(string.Format(
                Errores.ArgumentosIncorrectos,
                nombre.Texto,
                maximo is null ? $"al menos {minimo}" : minimo.ToString(CultureInfo.InvariantCulture),
                argumentos.Count));

        return new Llamada(nombre.Texto, argumentos);
    }

    /// <summary>Vuelve a escribir el árbol como texto. Sirve para enseñar la fórmula normalizada.</summary>
    public static string Escribir(Nodo nodo)
    {
        var sb = new StringBuilder();
        Escribir(nodo, sb);
        return sb.ToString();
    }

    private static void Escribir(Nodo nodo, StringBuilder sb)
    {
        switch (nodo)
        {
            case Constante c:
                sb.Append(c.Valor.ToString(CultureInfo.InvariantCulture));
                break;
            case Referencia r:
                sb.Append('[').Append(r.Nombre).Append(']');
                break;
            case Negacion n:
                sb.Append('-');
                Escribir(n.Interior, sb);
                break;
            case Operacion o:
                sb.Append('(');
                Escribir(o.Izquierda, sb);
                sb.Append(' ').Append(o.Operador).Append(' ');
                Escribir(o.Derecha, sb);
                sb.Append(')');
                break;
            case Llamada l:
                sb.Append(l.Funcion).Append('(');
                for (var i = 0; i < l.Argumentos.Count; i++)
                {
                    if (i > 0) sb.Append("; ");
                    Escribir(l.Argumentos[i], sb);
                }
                sb.Append(')');
                break;
        }
    }

    public static class Errores
    {
        public const string Vacia = "La fórmula está vacía";
        public const string CorcheteSinCerrar = "Falta cerrar un corchete de referencia: «[»";
        public const string ReferenciaVacia = "Hay una referencia sin nombre de campo: «[]»";
        public const string ParentesisSinCerrar = "Falta cerrar un paréntesis";
        public const string TerminaAntesDeTiempo = "La fórmula termina antes de tiempo";
        public const string ComparacionEncadenada =
            "No se pueden encadenar comparaciones. En vez de «a < b < c», escribe «SI(a < b; SI(b < c; 1; 0); 0)»";

        public const string DemasiadoLarga = "La fórmula no puede pasar de {0} caracteres";
        public const string DemasiadoCompleja = "La fórmula es demasiado compleja (más de {0} operaciones)";
        public const string CaracterInesperado = "No se entiende el carácter «{0}» (posición {1})";
        public const string NumeroMalEscrito = "«{0}» no es un número válido";
        public const string SobraTexto = "Sobra «{0}» al final de la fórmula";
        public const string SeEsperabaValor = "Se esperaba un número, un campo o un paréntesis, y se encontró «{0}»";
        public const string FuncionDesconocida = "La función «{0}» no existe. Las que hay: {1}";
        public const string FaltaParentesis = "A la función «{0}» le falta el paréntesis de apertura";
        public const string ArgumentosIncorrectos = "«{0}» necesita {1} argumentos y se le dieron {2}";
    }
}

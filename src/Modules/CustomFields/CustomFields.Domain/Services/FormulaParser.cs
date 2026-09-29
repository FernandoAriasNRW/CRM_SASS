using System.Globalization;
using System.Text;

namespace CustomFields.Domain.Services;

/// <summary>
/// Convierte el texto de una fórmula en un árbol que se puede evaluar, o dice por qué no.
///
/// Es una función pura y vive en el dominio, como <see cref="ValueValidator"/> y por el mismo
/// motivo: es donde se cuela la basura. Una fórmula mal escrita tiene que fallar **al
/// guardarla**, con un mensaje que diga dónde, y no meses después al abrir una tarea.
///
/// ── Decisiones del lenguaje ────────────────────────────────────────────────────────────────
///
/// **El separador de argumentos es `;` y no `,`.** En español la coma es el separador decimal y
/// quien escriba `REDONDEAR(1,5; 0)` espera un número y medio, no dos argumentos. Elegir la
/// coma obligaría a prohibir la coma decimal, que es justo lo que <see cref="ValueValidator"/>
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
public static class FormulaParser
{
    public const int MaxLength = 500;

    /// <summary>Cuántos nodos puede tener el árbol. Frena una fórmula escrita para hacer daño.</summary>
    public const int MaxNodes = 200;

    // ── El árbol ───────────────────────────────────────────────────────────────────────────

    public abstract record Node;

    public sealed record Constant(decimal Value) : Node;

    /// <summary>Una referencia a otro campo, por su nombre tal como se escribió.</summary>
    public sealed record Reference(string Name) : Node;

    public sealed record Operation(string Operator, Node Left, Node Right) : Node;

    public sealed record Negation(Node Operand) : Node;

    public sealed record Call(string Function, IReadOnlyList<Node> Arguments) : Node;

    public sealed record ParseResult(bool IsValid, Node? Tree, string? Error)
    {
        public static ParseResult Ok(Node tree) => new(true, tree, null);
        public static ParseResult Fail(string error) => new(false, null, error);
    }

    /// <summary>Las funciones que existen, con cuántos argumentos aceptan.</summary>
    public static class Functions
    {
        public const string If = "SI";
        public const string Round = "REDONDEAR";
        public const string Min = "MIN";
        public const string Max = "MAX";
        public const string Abs = "ABS";

        public static IReadOnlyList<string> All() => [If, Round, Min, Max, Abs];

        /// <summary>
        /// Cuántos argumentos admite. `null` en el segundo elemento significa «sin tope»: MIN y
        /// MAX aceptan los que sean, que es como se usan.
        /// </summary>
        public static (int Min, int? Max)? Arity(string function) => function switch
        {
            If => (3, 3),
            Round => (2, 2),
            Min or Max => (2, null),
            Abs => (1, 1),
            _ => null,
        };
    }

    // ── Entrada pública ────────────────────────────────────────────────────────────────────

    public static ParseResult Parse(string? formula)
    {
        var text = (formula ?? string.Empty).Trim();

        if (text.Length == 0)
            return ParseResult.Fail(Errors.Empty);

        if (text.Length > MaxLength)
            return ParseResult.Fail(string.Format(Errors.TooLong, MaxLength));

        var tokens = Tokenize(text, out var errorLexico);
        if (errorLexico is not null)
            return ParseResult.Fail(errorLexico);

        var reader = new Reader(tokens!);

        Node tree;
        try
        {
            tree = ReadComparison(reader);
        }
        catch (InvalidFormulaException ex)
        {
            return ParseResult.Fail(ex.Message);
        }

        // Si sobra algo después de la expresión, la fórmula está mal aunque el principio se
        // haya podido leer: `2 + 3 )` no es «2 + 3».
        if (!reader.IsAtEnd)
            return ParseResult.Fail(string.Format(Errors.TrailingText, reader.Current!.Text));

        return CountNodes(tree) > MaxNodes
            ? ParseResult.Fail(string.Format(Errors.TooComplex, MaxNodes))
            : ParseResult.Ok(tree);
    }

    /// <summary>
    /// Los campos a los que apunta una fórmula, sin repetir.
    ///
    /// Lo usa la detección de ciclos y la de referencias a campos que no existen. Se saca del
    /// árbol y no con una expresión regular sobre el texto: un corchete dentro de lo que sea
    /// daría una referencia fantasma.
    /// </summary>
    public static IReadOnlyList<string> ReferencesOf(Node tree)
    {
        var found = new List<string>();
        Walk(tree, n =>
        {
            if (n is Reference r && !found.Contains(r.Name, StringComparer.OrdinalIgnoreCase))
                found.Add(r.Name);
        });
        return found;
    }

    public static void Walk(Node node, Action<Node> visit)
    {
        visit(node);

        switch (node)
        {
            case Operation o:
                Walk(o.Left, visit);
                Walk(o.Right, visit);
                break;
            case Negation n:
                Walk(n.Operand, visit);
                break;
            case Call l:
                foreach (var a in l.Arguments) Walk(a, visit);
                break;
        }
    }

    private static int CountNodes(Node tree)
    {
        var count = 0;
        Walk(tree, _ => count++);
        return count;
    }

    // ── Análisis léxico ────────────────────────────────────────────────────────────────────

    private enum TokenKind { Number, Reference, Identifier, Operator, OpenParen, CloseParen, Separator }

    private sealed record Token(TokenKind TokenKind, string Text, int Position);

    private static List<Token>? Tokenize(string text, out string? error)
    {
        error = null;
        var tokens = new List<Token>();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || c == '.' || c == ',')
            {
                var start = i;
                var hasDecimal = false;

                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' || text[i] == ','))
                {
                    if (text[i] is '.' or ',')
                    {
                        // Dos separadores decimales en el mismo número es un error de quien
                        // escribe, no un número raro. Detectarlo aquí da un mensaje concreto;
                        // dejarlo pasar daría un fallo de conversión sin posición.
                        if (hasDecimal) { error = string.Format(Errors.MalformedNumber, text[start..(i + 1)]); return null; }
                        hasDecimal = true;
                    }
                    i++;
                }

                tokens.Add(new Token(TokenKind.Number, text[start..i], start));
                continue;
            }

            if (c == '[')
            {
                var closing = text.IndexOf(']', i + 1);
                if (closing < 0) { error = Errors.UnclosedBracket; return null; }

                var name = text[(i + 1)..closing].Trim();
                if (name.Length == 0) { error = Errors.EmptyReference; return null; }

                tokens.Add(new Token(TokenKind.Reference, name, i));
                i = closing + 1;
                continue;
            }

            if (char.IsLetter(c))
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                tokens.Add(new Token(TokenKind.Identifier, text[start..i].ToUpperInvariant(), start));
                continue;
            }

            // Los de dos caracteres primero: si no, `<=` se leería como `<` seguido de `=`.
            if (i + 1 < text.Length)
            {
                var two = text.Substring(i, 2);
                if (two is "<=" or ">=" or "<>")
                {
                    tokens.Add(new Token(TokenKind.Operator, two, i));
                    i += 2;
                    continue;
                }
            }

            switch (c)
            {
                case '+' or '-' or '*' or '/' or '<' or '>' or '=':
                    tokens.Add(new Token(TokenKind.Operator, c.ToString(), i)); i++; continue;
                case '(':
                    tokens.Add(new Token(TokenKind.OpenParen, "(", i)); i++; continue;
                case ')':
                    tokens.Add(new Token(TokenKind.CloseParen, ")", i)); i++; continue;
                case ';':
                    tokens.Add(new Token(TokenKind.Separator, ";", i)); i++; continue;
                default:
                    error = string.Format(Errors.UnexpectedCharacter, c, i + 1);
                    return null;
            }
        }

        return tokens.Count == 0 ? null : tokens;
    }

    // ── Análisis sintáctico, descendente recursivo ─────────────────────────────────────────

    private sealed class Reader(List<Token> tokens)
    {
        private int _i;

        public bool IsAtEnd => _i >= tokens.Count;
        public Token? Current => _i < tokens.Count ? tokens[_i] : null;

        public Token Consume() => tokens[_i++];

        public bool NextIs(TokenKind kind, params string[] texts) =>
            Current is { } t && t.TokenKind == kind && (texts.Length == 0 || texts.Contains(t.Text));
    }

    private sealed class InvalidFormulaException(string message) : Exception(message);

    /// <summary>
    /// Comparación. Va arriba del todo porque es la de menor precedencia y **no encadena**:
    /// `1 &lt; 2 &lt; 3` es un error, no una cadena que se evalúa por pares como en algunos
    /// lenguajes. Encadenarla sin darse cuenta produce resultados que parecen correctos.
    /// </summary>
    private static Node ReadComparison(Reader reader)
    {
        var left = ReadSum(reader);

        if (!reader.NextIs(TokenKind.Operator, "=", "<>", "<", "<=", ">", ">="))
            return left;

        var op = reader.Consume().Text;
        var right = ReadSum(reader);

        if (reader.NextIs(TokenKind.Operator, "=", "<>", "<", "<=", ">", ">="))
            throw new InvalidFormulaException(Errors.ChainedComparison);

        return new Operation(op, left, right);
    }

    private static Node ReadSum(Reader reader)
    {
        var node = ReadProduct(reader);

        while (reader.NextIs(TokenKind.Operator, "+", "-"))
        {
            var op = reader.Consume().Text;
            node = new Operation(op, node, ReadProduct(reader));
        }

        return node;
    }

    private static Node ReadProduct(Reader reader)
    {
        var node = ReadUnary(reader);

        while (reader.NextIs(TokenKind.Operator, "*", "/"))
        {
            var op = reader.Consume().Text;
            node = new Operation(op, node, ReadUnary(reader));
        }

        return node;
    }

    private static Node ReadUnary(Reader reader)
    {
        if (!reader.NextIs(TokenKind.Operator, "-"))
            return ReadPrimary(reader);

        reader.Consume();
        return new Negation(ReadUnary(reader));
    }

    private static Node ReadPrimary(Reader reader)
    {
        if (reader.IsAtEnd)
            throw new InvalidFormulaException(Errors.UnexpectedEnd);

        var token = reader.Consume();

        switch (token.TokenKind)
        {
            case TokenKind.Number:
                // La coma decimal se admite al escribir y se lee como punto, igual que en
                // ValidadorDeValor: quien escribe en español pone «1,5».
                var normalized = token.Text.Replace(',', '.');
                return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                    ? new Constant(number)
                    : throw new InvalidFormulaException(string.Format(Errors.MalformedNumber, token.Text));

            case TokenKind.Reference:
                return new Reference(token.Text);

            case TokenKind.OpenParen:
                var inner = ReadComparison(reader);
                if (!reader.NextIs(TokenKind.CloseParen))
                    throw new InvalidFormulaException(Errors.UnclosedParenthesis);
                reader.Consume();
                return inner;

            case TokenKind.Identifier:
                return ReadCall(reader, token);

            default:
                throw new InvalidFormulaException(string.Format(Errors.ExpectedValue, token.Text));
        }
    }

    private static Node ReadCall(Reader reader, Token name)
    {
        var aridad = Functions.Arity(name.Text);
        if (aridad is null)
            throw new InvalidFormulaException(string.Format(
                Errors.UnknownFunction, name.Text, string.Join(", ", Functions.All())));

        if (!reader.NextIs(TokenKind.OpenParen))
            throw new InvalidFormulaException(string.Format(Errors.MissingParenthesis, name.Text));
        reader.Consume();

        var arguments = new List<Node>();

        if (!reader.NextIs(TokenKind.CloseParen))
        {
            arguments.Add(ReadComparison(reader));

            while (reader.NextIs(TokenKind.Separator))
            {
                reader.Consume();
                arguments.Add(ReadComparison(reader));
            }
        }

        if (!reader.NextIs(TokenKind.CloseParen))
            throw new InvalidFormulaException(Errors.UnclosedParenthesis);
        reader.Consume();

        var (min, max) = aridad.Value;

        if (arguments.Count < min || (max is not null && arguments.Count > max))
            throw new InvalidFormulaException(string.Format(
                Errors.WrongArgumentCount,
                name.Text,
                max is null ? $"al menos {min}" : min.ToString(CultureInfo.InvariantCulture),
                arguments.Count));

        return new Call(name.Text, arguments);
    }

    /// <summary>Vuelve a escribir el árbol como texto. Sirve para enseñar la fórmula normalizada.</summary>
    public static string Write(Node node)
    {
        var sb = new StringBuilder();
        Write(node, sb);
        return sb.ToString();
    }

    private static void Write(Node node, StringBuilder sb)
    {
        switch (node)
        {
            case Constant c:
                sb.Append(c.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case Reference r:
                sb.Append('[').Append(r.Name).Append(']');
                break;
            case Negation n:
                sb.Append('-');
                Write(n.Operand, sb);
                break;
            case Operation o:
                sb.Append('(');
                Write(o.Left, sb);
                sb.Append(' ').Append(o.Operator).Append(' ');
                Write(o.Right, sb);
                sb.Append(')');
                break;
            case Call l:
                sb.Append(l.Function).Append('(');
                for (var i = 0; i < l.Arguments.Count; i++)
                {
                    if (i > 0) sb.Append("; ");
                    Write(l.Arguments[i], sb);
                }
                sb.Append(')');
                break;
        }
    }

    public static class Errors
    {
        public const string Empty = "La fórmula está vacía";
        public const string UnclosedBracket = "Falta cerrar un corchete de referencia: «[»";
        public const string EmptyReference = "Hay una referencia sin nombre de campo: «[]»";
        public const string UnclosedParenthesis = "Falta cerrar un paréntesis";
        public const string UnexpectedEnd = "La fórmula termina antes de tiempo";
        public const string ChainedComparison =
            "No se pueden encadenar comparaciones. En vez de «a < b < c», escribe «SI(a < b; SI(b < c; 1; 0); 0)»";

        public const string TooLong = "La fórmula no puede pasar de {0} caracteres";
        public const string TooComplex = "La fórmula es demasiado compleja (más de {0} operaciones)";
        public const string UnexpectedCharacter = "No se entiende el carácter «{0}» (posición {1})";
        public const string MalformedNumber = "«{0}» no es un número válido";
        public const string TrailingText = "Sobra «{0}» al final de la fórmula";
        public const string ExpectedValue = "Se esperaba un número, un campo o un paréntesis, y se encontró «{0}»";
        public const string UnknownFunction = "La función «{0}» no existe. Las que hay: {1}";
        public const string MissingParenthesis = "A la función «{0}» le falta el paréntesis de apertura";
        public const string WrongArgumentCount = "«{0}» necesita {1} argumentos y se le dieron {2}";
    }
}

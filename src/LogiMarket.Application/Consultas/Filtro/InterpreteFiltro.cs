using System.Globalization;
using LogiMarket.Domain.Pedidos.Estados;

namespace LogiMarket.Application.Consultas.Filtro;

/// <summary>Datos de un pedido sobre los que se evalúa el filtro.</summary>
public interface IPedidoFiltrable
{
    string Estado { get; }

    decimal Total { get; }

    string Cliente { get; }
}

/// <summary>Error de sintaxis del filtro con posición (columna 1-based) para un mensaje legible.</summary>
public sealed class FiltroInvalidoException : Exception
{
    public FiltroInvalidoException(string mensaje, int posicion)
        : base($"Error de sintaxis en la posición {posicion}: {mensaje}")
    {
        Posicion = posicion;
    }

    public int Posicion { get; }
}

// ---------------------------------------------------------------- Expresiones (árbol del Interpreter)

/// <summary>Expresión abstracta del Interpreter.</summary>
public interface IExpresionFiltro
{
    bool Interpretar(IPedidoFiltrable pedido);

    string Describir();
}

public sealed class ExpresionEstado(string estado) : IExpresionFiltro
{
    public bool Interpretar(IPedidoFiltrable p) => string.Equals(p.Estado, estado, StringComparison.OrdinalIgnoreCase);

    public string Describir() => $"ESTADO = {estado}";
}

public sealed class ExpresionCliente(string texto) : IExpresionFiltro
{
    /// <summary>Coincidencia sin distinguir mayúsculas: el nombre contiene el texto.</summary>
    public bool Interpretar(IPedidoFiltrable p) => p.Cliente.Contains(texto, StringComparison.OrdinalIgnoreCase);

    public string Describir() => $"CLIENTE = \"{texto}\"";
}

public sealed class ExpresionTotal(string operador, decimal valor) : IExpresionFiltro
{
    public bool Interpretar(IPedidoFiltrable p) => operador switch
    {
        "=" => p.Total == valor,
        ">=" => p.Total >= valor,
        "<=" => p.Total <= valor,
        ">" => p.Total > valor,
        "<" => p.Total < valor,
        _ => false,
    };

    public string Describir() => $"TOTAL {operador} {valor.ToString("0.00", CultureInfo.InvariantCulture)}";
}

public sealed class ExpresionY(IExpresionFiltro izquierda, IExpresionFiltro derecha) : IExpresionFiltro
{
    public bool Interpretar(IPedidoFiltrable p) => izquierda.Interpretar(p) && derecha.Interpretar(p);

    public string Describir() => $"({izquierda.Describir()} AND {derecha.Describir()})";
}

public sealed class ExpresionO(IExpresionFiltro izquierda, IExpresionFiltro derecha) : IExpresionFiltro
{
    public bool Interpretar(IPedidoFiltrable p) => izquierda.Interpretar(p) || derecha.Interpretar(p);

    public string Describir() => $"({izquierda.Describir()} OR {derecha.Describir()})";
}

// ---------------------------------------------------------------- Analizador léxico

internal enum TipoToken
{
    Identificador,
    Numero,
    Texto,
    Operador,
    And,
    Or,
    ParentesisAbre,
    ParentesisCierra,
    Fin,
}

internal sealed record Token(TipoToken Tipo, string Valor, int Posicion);

internal static class Lexer
{
    public static List<Token> Tokenizar(string entrada)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < entrada.Length)
        {
            var c = entrada[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '(')
            {
                tokens.Add(new Token(TipoToken.ParentesisAbre, "(", i + 1));
                i++;
            }
            else if (c == ')')
            {
                tokens.Add(new Token(TipoToken.ParentesisCierra, ")", i + 1));
                i++;
            }
            else if (c is '>' or '<' or '=')
            {
                var op = i + 1 < entrada.Length && entrada[i + 1] == '=' && c != '=' ? entrada.Substring(i, 2) : c.ToString();
                tokens.Add(new Token(TipoToken.Operador, op, i + 1));
                i += op.Length;
            }
            else if (c is '"' or '\'')
            {
                var fin = entrada.IndexOf(c, i + 1);
                if (fin < 0)
                {
                    throw new FiltroInvalidoException("comilla sin cerrar.", i + 1);
                }

                tokens.Add(new Token(TipoToken.Texto, entrada[(i + 1)..fin], i + 1));
                i = fin + 1;
            }
            else if (char.IsDigit(c))
            {
                var inicio = i;
                while (i < entrada.Length && (char.IsDigit(entrada[i]) || entrada[i] == '.'))
                {
                    i++;
                }

                tokens.Add(new Token(TipoToken.Numero, entrada[inicio..i], inicio + 1));
            }
            else if (char.IsLetter(c) || c == '_')
            {
                var inicio = i;
                while (i < entrada.Length && (char.IsLetterOrDigit(entrada[i]) || entrada[i] is '_' or '-' or '.'))
                {
                    i++;
                }

                var palabra = entrada[inicio..i];
                var tipo = palabra.ToUpperInvariant() switch
                {
                    "AND" => TipoToken.And,
                    "OR" => TipoToken.Or,
                    _ => TipoToken.Identificador,
                };
                tokens.Add(new Token(tipo, palabra, inicio + 1));
            }
            else
            {
                throw new FiltroInvalidoException($"carácter no reconocido '{c}'.", i + 1);
            }
        }

        tokens.Add(new Token(TipoToken.Fin, string.Empty, entrada.Length + 1));
        return tokens;
    }
}

// ---------------------------------------------------------------- Analizador sintáctico

/// <summary>
/// Parser descendente recursivo. Gramática (precedencia: paréntesis &gt; comparación &gt; AND &gt; OR):
/// <code>
/// expr   := termino ( OR termino )*
/// termino:= factor ( AND factor )*
/// factor := '(' expr ')' | comparacion
/// comparacion := ESTADO '=' palabra | CLIENTE '=' (palabra|texto) | TOTAL op numero
/// </code>
/// </summary>
public static class InterpreteFiltro
{
    private static readonly IReadOnlyList<string> Estados = NombresEstado.Todos;

    public static IExpresionFiltro Analizar(string filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro))
        {
            throw new FiltroInvalidoException("el filtro está vacío.", 1);
        }

        var parser = new Parser(Lexer.Tokenizar(filtro));
        var expresion = parser.Expr();
        parser.Esperar(TipoToken.Fin, "se esperaba el fin del filtro");
        return expresion;
    }

    public static IReadOnlyList<T> Filtrar<T>(IEnumerable<T> pedidos, string filtro)
        where T : IPedidoFiltrable
    {
        var expresion = Analizar(filtro);
        return pedidos.Where(p => expresion.Interpretar(p)).ToList();
    }

    private sealed class Parser(List<Token> tokens)
    {
        private int _pos;

        private Token Actual => tokens[_pos];

        public IExpresionFiltro Expr()
        {
            var izquierda = Termino();
            while (Actual.Tipo == TipoToken.Or)
            {
                _pos++;
                izquierda = new ExpresionO(izquierda, Termino());
            }

            return izquierda;
        }

        public Token Esperar(TipoToken tipo, string mensaje)
        {
            if (Actual.Tipo != tipo)
            {
                throw new FiltroInvalidoException($"{mensaje}; se encontró '{(Actual.Tipo == TipoToken.Fin ? "fin" : Actual.Valor)}'.", Actual.Posicion);
            }

            return tokens[_pos++];
        }

        private IExpresionFiltro Termino()
        {
            var izquierda = Factor();
            while (Actual.Tipo == TipoToken.And)
            {
                _pos++;
                izquierda = new ExpresionY(izquierda, Factor());
            }

            return izquierda;
        }

        private IExpresionFiltro Factor()
        {
            if (Actual.Tipo == TipoToken.ParentesisAbre)
            {
                _pos++;
                var interna = Expr();
                Esperar(TipoToken.ParentesisCierra, "falta ')'");
                return interna;
            }

            return Comparacion();
        }

        private IExpresionFiltro Comparacion()
        {
            var campo = Esperar(TipoToken.Identificador, "se esperaba ESTADO, TOTAL o CLIENTE");
            var op = Esperar(TipoToken.Operador, $"se esperaba un operador después de {campo.Valor}");
            switch (campo.Valor.ToUpperInvariant())
            {
                case "ESTADO":
                {
                    ExigirIgual(op, "ESTADO");
                    var valor = Esperar(TipoToken.Identificador, "se esperaba un estado");
                    var estado = valor.Valor.ToUpperInvariant();
                    if (!Estados.Contains(estado))
                    {
                        throw new FiltroInvalidoException($"estado desconocido '{valor.Valor}'. Válidos: {string.Join(", ", Estados)}.", valor.Posicion);
                    }

                    return new ExpresionEstado(estado);
                }

                case "CLIENTE":
                {
                    ExigirIgual(op, "CLIENTE");
                    var valor = Actual.Tipo is TipoToken.Texto or TipoToken.Identificador
                        ? tokens[_pos++]
                        : throw new FiltroInvalidoException("se esperaba el nombre del cliente.", Actual.Posicion);
                    return new ExpresionCliente(valor.Valor);
                }

                case "TOTAL":
                {
                    var numero = Esperar(TipoToken.Numero, "se esperaba un número después de TOTAL");
                    if (!decimal.TryParse(numero.Valor, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor))
                    {
                        throw new FiltroInvalidoException($"número inválido '{numero.Valor}'.", numero.Posicion);
                    }

                    return new ExpresionTotal(op.Valor, valor);
                }

                default:
                    throw new FiltroInvalidoException($"campo desconocido '{campo.Valor}'. Use ESTADO, TOTAL o CLIENTE.", campo.Posicion);
            }
        }

        private static void ExigirIgual(Token op, string campo)
        {
            if (op.Valor != "=")
            {
                throw new FiltroInvalidoException($"{campo} solo admite el operador '='.", op.Posicion);
            }
        }
    }
}

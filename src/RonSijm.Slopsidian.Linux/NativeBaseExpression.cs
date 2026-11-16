using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RonSijm.Slopsidian.Linux;

public abstract record NativeBaseExpression
{
    public sealed record Literal(object? Value) : NativeBaseExpression;

    public sealed record Name(string Value) : NativeBaseExpression;

    public sealed record List(IReadOnlyList<NativeBaseExpression> Items) : NativeBaseExpression;

    public sealed record Member(NativeBaseExpression Receiver, NativeBaseExpression Key) : NativeBaseExpression;

    public sealed record Invocation(NativeBaseExpression Callee, IReadOnlyList<NativeBaseExpression> Arguments)
        : NativeBaseExpression;

    public sealed record Unary(string Operator, NativeBaseExpression Operand) : NativeBaseExpression;

    public sealed record Binary(string Operator, NativeBaseExpression Left, NativeBaseExpression Right)
        : NativeBaseExpression;

    public static NativeBaseExpression Parse(string source) => new NativeBaseParser(source).Parse();
}

internal sealed class NativeBaseParser
{
    private sealed record Token(string Kind, string Text, object? Value, int Offset);

    private static readonly Regex NumberPattern = new(
        @"\G(?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private static readonly Regex NamePattern = new(
        @"\G[a-zA-Z_$][a-zA-Z0-9_$]*",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private static readonly Regex OperatorPattern = new(
        @"\G(?:&&|\|\||==|!=|<=|>=|[+\-*/%<>()\[\].,!])",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private readonly List<Token> tokens = [];
    private int index;

    public NativeBaseParser(string source)
    {
        if (source.Length > 4096)
        {
            throw new InvalidDataException("A formula contains at most 4,096 characters.");
        }
        Tokenize(source);
    }

    public NativeBaseExpression Parse()
    {
        var result = Expression(0, 0);
        if (Peek.Kind != "end")
        {
            throw Error("Unexpected formula syntax");
        }
        return result;
    }

    private Token Peek => tokens[index];

    private InvalidDataException Error(string message) => new(message + " at character " + Peek.Offset + ".");

    private void Consume(string text)
    {
        if (Peek.Text != text)
        {
            throw Error("Expected " + text);
        }
        index++;
    }

    private NativeBaseExpression Expression(int minimum, int depth)
    {
        if (depth > 48)
        {
            throw new InvalidDataException("Formula nesting exceeds 48 levels.");
        }
        var left = Prefix(depth);
        while (true)
        {
            if (Peek.Text is "." or "[" or "(")
            {
                left = Postfix(left, depth);
                continue;
            }
            var rank = Precedence(Peek.Text);
            if (rank < minimum)
            {
                return left;
            }
            var operation = tokens[index++].Text;
            left = new NativeBaseExpression.Binary(operation, left, Expression(rank + 1, depth + 1));
        }
    }

    private NativeBaseExpression Prefix(int depth)
    {
        var token = Peek;
        if (token.Kind == "end")
        {
            throw Error("Expected a value");
        }
        index++;
        if (token.Kind == "value")
        {
            return new NativeBaseExpression.Literal(token.Value);
        }
        if (token.Kind == "name")
        {
            return new NativeBaseExpression.Name(token.Text);
        }
        if (token.Text is "!" or "-" or "+")
        {
            return new NativeBaseExpression.Unary(token.Text, Expression(7, depth + 1));
        }
        if (token.Text == "(")
        {
            var result = Expression(0, depth + 1);
            Consume(")");
            return result;
        }
        if (token.Text == "[")
        {
            return new NativeBaseExpression.List(Arguments("]", 128, depth));
        }
        throw new InvalidDataException("Expected a value at character " + token.Offset + ".");
    }

    private NativeBaseExpression Postfix(NativeBaseExpression left, int depth)
    {
        var operation = tokens[index++].Text;
        if (operation == "(")
        {
            return new NativeBaseExpression.Invocation(left, Arguments(")", 32, depth));
        }
        if (operation == "[")
        {
            var key = Expression(0, depth + 1);
            Consume("]");
            return new NativeBaseExpression.Member(left, key);
        }
        if (Peek.Kind != "name")
        {
            throw Error("Expected a property name after the dot");
        }
        return new NativeBaseExpression.Member(left, new NativeBaseExpression.Literal(tokens[index++].Text));
    }

    private List<NativeBaseExpression> Arguments(string closing, int maximum, int depth)
    {
        var result = new List<NativeBaseExpression>();
        if (Peek.Text != closing)
        {
            do
            {
                if (result.Count > 0)
                {
                    Consume(",");
                }
                result.Add(Expression(0, depth + 1));
                if (result.Count > maximum)
                {
                    throw new InvalidDataException("This formula list or function has too many arguments.");
                }
            } while (Peek.Text == ",");
        }
        Consume(closing);
        return result;
    }

    private static int Precedence(string text) =>
        text switch
        {
            "||" => 1,
            "&&" => 2,
            "==" or "!=" => 3,
            ">" or "<" or ">=" or "<=" => 4,
            "+" or "-" => 5,
            "*" or "/" or "%" => 6,
            _ => -1,
        };

    private void Tokenize(string source)
    {
        var position = 0;
        while (position < source.Length)
        {
            if (char.IsWhiteSpace(source[position]))
            {
                position++;
                continue;
            }
            var offset = position;
            if (source[position] is '\'' or '"')
            {
                var value = Quoted(source, ref position);
                tokens.Add(new Token("value", source[offset..position], value, offset));
            }
            else
            {
                tokens.Add(NextToken(source, ref position));
            }
            if (tokens.Count > 1024)
            {
                throw new InvalidDataException("A formula exceeds 1,024 tokens.");
            }
        }
        tokens.Add(new Token("end", "", null, position));
    }

    private static Token NextToken(string source, ref int position)
    {
        var offset = position;
        var number = NumberPattern.Match(source, position);
        if (number.Success)
        {
            position += number.Length;
            var value = NativeBaseValues.Number(
                double.Parse(number.Value, NumberStyles.Float, CultureInfo.InvariantCulture)
            );
            return new Token("value", number.Value, value, offset);
        }
        var name = NamePattern.Match(source, position);
        if (name.Success)
        {
            position += name.Length;
            NativeBaseValues.CheckName(name.Value);
            return name.Value switch
            {
                "true" => new Token("value", name.Value, true, offset),
                "false" => new Token("value", name.Value, false, offset),
                "null" => new Token("value", name.Value, null, offset),
                _ => new Token("name", name.Value, null, offset),
            };
        }
        var symbol = OperatorPattern.Match(source, position);
        if (!symbol.Success)
        {
            throw new InvalidDataException("Unsupported formula syntax at character " + position + ".");
        }
        position += symbol.Length;
        return new Token("symbol", symbol.Value, null, offset);
    }

    private static string Quoted(string source, ref int position)
    {
        var quote = source[position++];
        var result = new StringBuilder();
        while (position < source.Length && source[position] != quote)
        {
            var character = source[position++];
            result.Append(character == '\\' ? Escape(source, ref position) : character);
        }
        if (position == source.Length)
        {
            throw new InvalidDataException("Unclosed formula string.");
        }
        position++;
        return result.ToString();
    }

    private static char Escape(string source, ref int position)
    {
        if (position >= source.Length)
        {
            throw new InvalidDataException("Unclosed formula string escape.");
        }
        var character = source[position++];
        if (character == 'u')
        {
            if (
                position + 4 > source.Length
                || !ushort.TryParse(
                    source.AsSpan(position, 4),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var code
                )
            )
            {
                throw new InvalidDataException("Invalid Unicode escape in formula string.");
            }
            position += 4;
            return (char)code;
        }
        return character switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'b' => '\b',
            'f' => '\f',
            '\\' or '\'' or '"' => character,
            _ => throw new InvalidDataException("Unsupported formula string escape."),
        };
    }
}

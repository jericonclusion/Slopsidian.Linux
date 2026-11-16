using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace RonSijm.Slopsidian.Linux;

internal enum NativeYamlKind
{
    Scalar,
    Mapping,
    Sequence,
    Alias,
}

internal sealed record NativeYamlEntry(NativeYamlNode Key, NativeYamlNode Value);

internal sealed class NativeYamlNode(NativeYamlKind kind, int start, int column)
{
    public NativeYamlKind Kind { get; } = kind;
    public int Start { get; } = start;
    public int Column { get; } = column;
    public int End { get; set; }
    public int Closing { get; set; }
    public bool Flow { get; init; }
    public string Text { get; init; } = "";
    public string Tag { get; init; } = "";
    public ScalarStyle Style { get; init; }
    public NativeYamlNode? Target { get; init; }
    public Dictionary<string, NativeYamlEntry> Fields { get; } = new(StringComparer.Ordinal);
    public List<NativeYamlNode> Items { get; } = [];
    public NativeYamlNode Resolved => Kind == NativeYamlKind.Alias ? Target! : this;

    public NativeYamlNode? Field(string name) => Resolved.Fields.GetValueOrDefault(name)?.Value;
}

internal sealed partial class NativeYamlSource
{
    private readonly string bom;
    private readonly string body;
    private readonly Dictionary<string, NativeYamlNode> anchors = new(StringComparer.Ordinal);
    private int nodes;
    private int aliases;
    public string Source { get; }
    public string Newline { get; }
    public NativeYamlNode Root { get; }

    public NativeYamlSource(string source)
    {
        if (source.Length > 1_048_576)
        {
            throw new InvalidDataException("A base contains at most 1 MiB of YAML.");
        }
        Source = source;
        bom = source.StartsWith('\ufeff') ? "\ufeff" : "";
        body = source[bom.Length..];
        Newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        try
        {
            var parser = new Parser(new StringReader(body));
            parser.Consume<StreamStart>();
            parser.Consume<DocumentStart>();
            Root = Read(parser, 0);
            parser.Consume<DocumentEnd>();
            if (!parser.Accept<StreamEnd>(out _))
            {
                throw new InvalidDataException("A base needs exactly one YAML document.");
            }
        }
        catch (YamlException exception)
        {
            throw new InvalidDataException("Invalid base YAML: " + exception.Message, exception);
        }
    }

    private NativeYamlNode Read(IParser parser, int depth)
    {
        if (depth > 64 || ++nodes > 100_000)
        {
            throw new InvalidDataException("Base YAML exceeds its nesting or node limit.");
        }
        var current = parser.Current ?? throw new InvalidDataException("Unexpected end of base YAML.");
        var start = checked((int)current.Start.Index);
        var column = start - (body.LastIndexOf('\n', Math.Max(0, start - 1)) + 1);
        if (current is AnchorAlias alias)
        {
            return ReadAlias(parser, alias, start, column);
        }
        if (current is Scalar scalar)
        {
            var result = new NativeYamlNode(NativeYamlKind.Scalar, start, column)
            {
                Text = scalar.Value,
                Style = scalar.Style,
                Tag = Tag(scalar),
                End = checked((int)scalar.End.Index),
            };
            if (
                result.Tag
                is "tag:yaml.org,2002:int"
                    or "tag:yaml.org,2002:float"
                    or "tag:yaml.org,2002:bool"
                    or "tag:yaml.org,2002:null"
            )
            {
                _ = NativeYamlScalar.Value(result);
            }
            Register(scalar, result);
            parser.MoveNext();
            return result;
        }
        return ReadContainer(parser, current, start, column, depth);
    }

    private NativeYamlNode ReadAlias(IParser parser, AnchorAlias alias, int start, int column)
    {
        if (++aliases > 32)
        {
            throw new InvalidDataException("A base supports at most 32 YAML aliases.");
        }
        if (!anchors.TryGetValue(alias.Value.Value, out var target))
        {
            throw new InvalidDataException("Unknown YAML alias: " + alias.Value);
        }
        var result = new NativeYamlNode(NativeYamlKind.Alias, start, column)
        {
            Target = target,
            End = checked((int)alias.End.Index),
        };
        parser.MoveNext();
        return result;
    }

    private NativeYamlNode ReadContainer(IParser parser, ParsingEvent current, int start, int column, int depth)
    {
        NativeYamlNode result = current switch
        {
            MappingStart mapping => new NativeYamlNode(NativeYamlKind.Mapping, start, column)
            {
                Flow = mapping.Style == MappingStyle.Flow,
                Tag = Tag(mapping),
            },
            SequenceStart sequence => new NativeYamlNode(NativeYamlKind.Sequence, start, column)
            {
                Flow = sequence.Style == SequenceStyle.Flow,
                Tag = Tag(sequence),
            },
            _ => throw new InvalidDataException("Unexpected YAML value."),
        };
        Register((NodeEvent)current, result);
        parser.MoveNext();
        if (result.Kind == NativeYamlKind.Mapping)
        {
            ReadMapping(parser, result, depth);
        }
        else
        {
            ReadSequence(parser, result, depth);
        }
        var ending = parser.Current ?? throw new InvalidDataException("Unexpected end of YAML collection.");
        result.Closing = checked((int)ending.Start.Index);
        if (result.Flow)
        {
            result.End = FlowEnd(ending, result.Kind == NativeYamlKind.Mapping ? '}' : ']');
        }
        parser.MoveNext();
        return result;
    }

    private void ReadMapping(IParser parser, NativeYamlNode result, int depth)
    {
        while (!parser.Accept<MappingEnd>(out _))
        {
            var key = Read(parser, depth + 1);
            if (key.Resolved.Kind != NativeYamlKind.Scalar)
            {
                throw new InvalidDataException("Base mapping keys must be scalar values.");
            }
            var value = Read(parser, depth + 1);
            if (!result.Fields.TryAdd(key.Resolved.Text, new NativeYamlEntry(key, value)))
            {
                throw new InvalidDataException("Duplicate YAML key: " + key.Resolved.Text);
            }
            result.End = value.End;
        }
    }

    private void ReadSequence(IParser parser, NativeYamlNode result, int depth)
    {
        while (!parser.Accept<SequenceEnd>(out _))
        {
            var value = Read(parser, depth + 1);
            result.Items.Add(value);
            result.End = value.End;
        }
    }

    private int FlowEnd(ParsingEvent ending, char delimiter)
    {
        var end = checked((int)ending.End.Index);
        return end < body.Length && body[end] == delimiter ? end + 1 : end;
    }

    private void Register(NodeEvent source, NativeYamlNode target)
    {
        if (!source.Anchor.IsEmpty)
        {
            anchors[source.Anchor.Value] = target;
        }
    }

    private static string Tag(NodeEvent source)
    {
        var tag = source.Tag.IsEmpty ? "" : source.Tag.Value;
        if (
            tag
            is not (
                ""
                or "!"
                or "tag:yaml.org,2002:str"
                or "tag:yaml.org,2002:int"
                or "tag:yaml.org,2002:float"
                or "tag:yaml.org,2002:bool"
                or "tag:yaml.org,2002:null"
                or "tag:yaml.org,2002:map"
                or "tag:yaml.org,2002:seq"
            )
        )
        {
            throw new InvalidDataException("Unsupported YAML tag: " + tag);
        }
        if (source is Scalar && tag is "tag:yaml.org,2002:map" or "tag:yaml.org,2002:seq")
        {
            throw new InvalidDataException("A scalar has an invalid collection tag.");
        }
        if (source is MappingStart && tag is not ("" or "!" or "tag:yaml.org,2002:map"))
        {
            throw new InvalidDataException("A mapping has an invalid YAML tag.");
        }
        if (source is SequenceStart && tag is not ("" or "!" or "tag:yaml.org,2002:seq"))
        {
            throw new InvalidDataException("A sequence has an invalid YAML tag.");
        }
        return tag;
    }
}

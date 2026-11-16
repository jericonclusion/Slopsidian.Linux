using System.Text.Json;

namespace RonSijm.Slopsidian.Linux;

internal sealed partial class NativeYamlSource
{
    public string SetField(NativeYamlNode mapping, string key, string? rawValue)
    {
        if (mapping.Kind != NativeYamlKind.Mapping)
        {
            throw new InvalidDataException("Edit this aliased configuration in the base source.");
        }
        if (mapping.Fields.TryGetValue(key, out var existing))
        {
            return bom
                + (
                    rawValue is null
                        ? RemoveField(mapping, existing)
                        : body[..existing.Value.Start] + rawValue + body[existing.Value.End..]
                );
        }
        if (rawValue is null)
        {
            return Source;
        }
        var field = JsonSerializer.Serialize(key) + ": " + rawValue;
        if (mapping.Flow)
        {
            var separator = mapping.Fields.Count == 0 ? "" : ", ";
            return bom + body[..mapping.Closing] + separator + field + body[mapping.Closing..];
        }
        return bom + InsertBlockField(mapping, field);
    }

    private string InsertBlockField(NativeYamlNode mapping, string field)
    {
        if (mapping.Fields.Count == 0)
        {
            throw new InvalidDataException("Edit this empty mapping in the base source.");
        }
        var entries = mapping.Fields.Values.OrderBy(entry => entry.Key.Start).ToArray();
        var last = entries[^1];
        var position = LineBoundary(last.Value.End);
        var indentation = new string(' ', entries[0].Key.Column);
        var prefix = position > 0 && body[position - 1] != '\n' ? Newline : "";
        var suffix = position < body.Length || body.EndsWith('\n') ? Newline : "";
        return body[..position] + prefix + indentation + field + suffix + body[position..];
    }

    private string RemoveField(NativeYamlNode mapping, NativeYamlEntry entry) =>
        mapping.Flow ? RemoveFlowField(mapping, entry) : RemoveBlockField(entry);

    private string RemoveBlockField(NativeYamlEntry entry)
    {
        var start = body.LastIndexOf('\n', Math.Max(0, entry.Key.Start - 1)) + 1;
        var end = LineBoundary(entry.Value.End);
        var prefix = body[start..entry.Key.Start];
        if (!prefix.All(char.IsWhiteSpace))
        {
            return body[..start] + prefix + Newline + body[end..];
        }
        if (end == body.Length && !body.EndsWith('\n'))
        {
            start = BeforeNewline(start);
        }
        return body[..start] + body[end..];
    }

    private string RemoveFlowField(NativeYamlNode mapping, NativeYamlEntry entry)
    {
        var entries = mapping.Fields.Values.OrderBy(item => item.Key.Start).ToArray();
        var index = Array.IndexOf(entries, entry);
        var start = entry.Key.Start;
        var end = entry.Value.End;
        if (index + 1 < entries.Length)
        {
            end = entries[index + 1].Key.Start;
        }
        else if (index > 0)
        {
            start = Comma(entries[index - 1].Value.End, entry.Key.Start);
        }
        return body[..start] + body[end..];
    }

    private int Comma(int start, int end)
    {
        var comment = false;
        for (var position = start; position < end; position++)
        {
            var character = body[position];
            if (character == '\n')
            {
                comment = false;
            }
            else if (character == '#')
            {
                comment = true;
            }
            else if (!comment && character == ',')
            {
                return position;
            }
        }
        throw new InvalidDataException("Cannot locate the YAML field separator. Edit this mapping in Source.");
    }

    private int LineBoundary(int position)
    {
        if (position >= body.Length || position > 0 && body[position - 1] == '\n')
        {
            return position;
        }
        var ending = body.IndexOf('\n', position);
        return ending < 0 ? body.Length : ending + 1;
    }

    private int BeforeNewline(int position)
    {
        if (position == 0 || body[position - 1] != '\n')
        {
            return position;
        }
        return position >= 2 && body[position - 2] == '\r' ? position - 2 : position - 1;
    }
}

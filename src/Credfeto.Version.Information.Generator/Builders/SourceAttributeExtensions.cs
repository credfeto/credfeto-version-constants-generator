using System.Collections.Immutable;

namespace Credfeto.Version.Information.Generator.Builders;

internal static class SourceAttributeExtensions
{
    public static CodeBuilder AppendAttributeValue(
        this CodeBuilder source,
        in ImmutableDictionary<string, string> attributes,
        string attributeName,
        string key
    )
    {
        return attributes.TryGetValue(key: attributeName, out string? value)
            ? source.AppendPublicConstant(key: key, value: value)
            : source;
    }

    public static CodeBuilder AppendPublicConstant(this CodeBuilder source, string key, string value)
    {
        return source.AppendLine($"public const string {key} = \"{value}\";");
    }
}

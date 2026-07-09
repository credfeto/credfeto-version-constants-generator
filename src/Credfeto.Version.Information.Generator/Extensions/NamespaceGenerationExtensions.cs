using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Credfeto.Version.Information.Generator.Builders;
using Credfeto.Version.Information.Generator.Models;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Credfeto.Version.Information.Generator.Extensions;

internal static class NamespaceGenerationExtensions
{
    private static string GetNamespace(in this NamespaceGeneration namespaceGeneration, string? rootNamespace)
    {
        return rootNamespace ?? namespaceGeneration.Namespace;
    }

    internal static string? GetRootNameSpace(AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
    {
        return
            analyzerConfigOptionsProvider.GlobalOptions.TryGetValue(key: "build_property.rootnamespace", out string? ns)
            && !string.IsNullOrWhiteSpace(ns)
            ? ns
            : null;
    }

    private static string GetAssemblyProduct(in this NamespaceGeneration namespaceGeneration, string? rootNamespace)
    {
        return rootNamespace ?? GetAssemblyTitle(namespaceGeneration) ?? namespaceGeneration.Namespace;
    }

    private static string? GetAssemblyTitle(in NamespaceGeneration namespaceGeneration)
    {
        return
            namespaceGeneration.Attributes.TryGetValue(nameof(AssemblyTitleAttribute), out string? product)
            && !string.IsNullOrWhiteSpace(product)
            ? product
            : null;
    }

    private static string GetAssemblyVersion(in this NamespaceGeneration namespaceGeneration)
    {
        return
            namespaceGeneration.Attributes.TryGetValue(
                nameof(AssemblyInformationalVersionAttribute),
                out string? version
            ) && !string.IsNullOrWhiteSpace(version)
            ? version
            : namespaceGeneration.Assembly.Version.ToString();
    }

    public static CodeBuilder BuildSource(
        in this NamespaceGeneration namespaceGeneration,
        string? rootNamespace,
        out string ns
    )
    {
        ns = namespaceGeneration.GetNamespace(rootNamespace: rootNamespace);
        string product = namespaceGeneration.GetAssemblyProduct(rootNamespace: rootNamespace);
        string version = RemoveGitHashFromVersion(namespaceGeneration.GetAssemblyVersion());

        CodeBuilder source = new();

        using (
            source
                .AppendFileHeader()
                .AppendLine("using System;")
                .AppendLine("using System.CodeDom.Compiler;")
                .AppendBlankLine()
                .AppendLine($"namespace {ns};")
                .AppendBlankLine()
                .AppendGeneratedCodeAttribute()
                .StartBlock("internal static class VersionInformation")
        )
        {
            DumpAttributes(attributes: namespaceGeneration.Attributes, source: source);

            source
                .AppendPublicConstant(key: "Version", value: version)
                .AppendPublicConstant(key: "Product", value: product)
                .AppendAttributeValue(
                    attributes: namespaceGeneration.Attributes,
                    nameof(AssemblyCompanyAttribute),
                    key: "Company"
                )
                .AppendAttributeValue(
                    attributes: namespaceGeneration.Attributes,
                    nameof(AssemblyCopyrightAttribute),
                    key: "Copyright"
                );
        }

        return source;
    }

    [Conditional("DEBUG")]
    private static void DumpAttributes(ImmutableDictionary<string, string> attributes, CodeBuilder source)
    {
        foreach (string key in attributes.Keys)
        {
            string value = attributes[key];

            source.AppendLine($"// {key} = {value}");
        }
    }

    private static string RemoveGitHashFromVersion(string source)
    {
        int pos = source.IndexOf(value: '+');

        return pos != -1 ? source.Substring(startIndex: 0, length: pos) : source;
    }
}

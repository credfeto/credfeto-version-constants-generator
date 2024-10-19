using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Credfeto.Version.Information.Generator.Builders;
using Credfeto.Version.Information.Generator.Models;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Credfeto.Version.Information.Generator.Extensions;

internal static class NamespaceGenerationExtensions
{
    private static string GetNamespace(in this NamespaceGeneration namespaceGeneration, in AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
    {
        return GetRootNameSpace(analyzerConfigOptionsProvider: analyzerConfigOptionsProvider) ?? namespaceGeneration.Namespace;
    }

    private static string? GetRootNameSpace(AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
    {
        if (analyzerConfigOptionsProvider.GlobalOptions.TryGetValue(key: "build_property.rootnamespace", out string? ns) && !string.IsNullOrWhiteSpace(ns))
        {
            return ns;
        }

        return null;
    }

    private static string GetAssemblyProduct(in this NamespaceGeneration namespaceGeneration, in AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
    {
        string? product = GetRootNameSpace(analyzerConfigOptionsProvider: analyzerConfigOptionsProvider);

        if (product is not null)
        {
            return product;
        }

        if (namespaceGeneration.Attributes.TryGetValue(nameof(AssemblyTitleAttribute), value: out product) && !string.IsNullOrWhiteSpace(product))
        {
            return product;
        }

        return namespaceGeneration.Namespace;
    }

    private static string GetAssemblyVersion(in this NamespaceGeneration namespaceGeneration)
    {
        return namespaceGeneration.Attributes.TryGetValue(nameof(AssemblyInformationalVersionAttribute), out string? version) && !string.IsNullOrWhiteSpace(version)
            ? version
            : namespaceGeneration.Assembly.Version.ToString();
    }

    public static CodeBuilder BuildSource(in this NamespaceGeneration namespaceGeneration, AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider, out string ns)
    {
        ns = namespaceGeneration.GetNamespace(analyzerConfigOptionsProvider: analyzerConfigOptionsProvider);
        string product = namespaceGeneration.GetAssemblyProduct(analyzerConfigOptionsProvider: analyzerConfigOptionsProvider);
        string version = CleanVersion(namespaceGeneration.GetAssemblyVersion());

        CodeBuilder source = new();

        source.AppendFileHeader()
              .AppendLine("using System;")
              .AppendLine("using System.CodeDom.Compiler;")
              .AppendBlankLine()
              .AppendLine($"namespace {ns};")
              .AppendBlankLine()
              .AppendGeneratedCodeAttribute();

        using (source.StartBlock("internal static class VersionInformation"))
        {
            DumpAttributes(attributes: namespaceGeneration.Attributes, source: source);

            source.AppendPublicConstant(key: "Version", value: version)
                  .AppendPublicConstant(key: "Product", value: product)
                  .AppendAttributeValue(attributes: namespaceGeneration.Attributes, nameof(AssemblyCompanyAttribute), key: "Company")
                  .AppendAttributeValue(attributes: namespaceGeneration.Attributes, nameof(AssemblyCopyrightAttribute), key: "Copyright");
        }

        return source;
    }

    [Conditional("DEBUG")]
    private static void DumpAttributes(ImmutableDictionary<string, string> attributes, CodeBuilder source)
    {
        foreach (string key in attributes.Keys)
        {
            if (!attributes.TryGetValue(key: key, out string? value))
            {
                continue;
            }

            source.AppendLine($"// {key} = {value}");
        }
    }

    private static string CleanVersion(string source)
    {
        int pos = source.IndexOf(value: '+');

        return pos != -1
            ? source.Substring(startIndex: 0, length: pos)
            : source;
    }
}
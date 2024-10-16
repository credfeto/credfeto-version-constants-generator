using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Credfeto.Version.Information.Generator.Builders;
using Credfeto.Version.Information.Generator.Helpers;
using Microsoft.CodeAnalysis;

namespace Credfeto.Version.Information.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class VersionInformationCodeGenerator : ISourceGenerator
{
    private const string CLASS_NAME = "VersionInformation";

    public void Initialize(GeneratorInitializationContext context)
    {
        // nothing to do here - no initialization required
    }

    public void Execute(GeneratorExecutionContext context)
    {
        ImmutableDictionary<string, string> attributes = ExtractAttributes(context);

        string assemblyNamespace = GetAssemblyNamespace(context);
        string product = GetAssemblyProduct(context: context, attributes: attributes, assemblyNamespace: assemblyNamespace);
        string version = CleanVersion(GetAssemblyVersion(context: context, attributes: attributes));

        CodeBuilder source = BuildSource(assemblyNamespace: assemblyNamespace, version: version, product: product, attributes: attributes);

        context.AddSource($"{assemblyNamespace}.{CLASS_NAME}.generated.cs", sourceText: source.Text);
    }

    private static CodeBuilder BuildSource(string assemblyNamespace, string version, string product, in ImmutableDictionary<string, string> attributes)
    {
        CodeBuilder source = new();

        source.AppendFileHeader()
              .AppendLine("using System;")
              .AppendLine("using System.CodeDom.Compiler;")
              .AppendBlankLine()
              .AppendLine($"namespace {assemblyNamespace};")
              .AppendBlankLine()
              .AppendLine($"[GeneratedCode(tool: \"{RuntimeVersionInformation.ToolName}\", version: \"{RuntimeVersionInformation.GeneratorVersion}\")]");

        using (source.StartBlock("internal static class VersionInformation"))
        {
            DumpAttributes(attributes: attributes, source: source);

            source.AppendPublicConstant(key: "Version", value: version)
                  .AppendPublicConstant(key: "Product", value: product)
                  .AppendAttributeValue(attributes: attributes, nameof(AssemblyCompanyAttribute), key: "Company")
                  .AppendAttributeValue(attributes: attributes, nameof(AssemblyCopyrightAttribute), key: "Copyright");
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

    private static ImmutableDictionary<string, string> ExtractAttributes(in GeneratorExecutionContext context)
    {
        IAssemblySymbol ass = context.Compilation.Assembly;
        ImmutableArray<AttributeData> attibuteData = ass.GetAttributes();

        ImmutableDictionary<string, string> attributes = ImmutableDictionary<string, string>.Empty;

        foreach (AttributeData a in attibuteData)
        {
            if (a.AttributeClass is null || a.ConstructorArguments.Length != 1)
            {
                continue;
            }

            string key = a.AttributeClass.Name;
            object? v = a.ConstructorArguments[0].Value;
            string value = v?.ToString() ?? string.Empty;

            attributes = attributes.Add(key: key, value: value);
        }

        return attributes;
    }

    private static string GetAssemblyProduct(in GeneratorExecutionContext context, ImmutableDictionary<string, string> attributes, string assemblyNamespace)
    {
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key: "build_property.rootnamespace", out string? product) && !string.IsNullOrWhiteSpace(product))
        {
            return product;
        }

        if (attributes.TryGetValue(nameof(AssemblyTitleAttribute), value: out product) && !string.IsNullOrWhiteSpace(product))
        {
            return product;
        }

        return assemblyNamespace;
    }

    private static string GetAssemblyVersion(in GeneratorExecutionContext context, ImmutableDictionary<string, string> attributes)
    {
        return attributes.TryGetValue(nameof(AssemblyInformationalVersionAttribute), out string? version) && !string.IsNullOrWhiteSpace(version)
            ? version
            : context.Compilation.Assembly.Identity.Version.ToString();
    }

    private static string GetAssemblyNamespace(in GeneratorExecutionContext context)
    {
        return context.Compilation.Assembly.Identity.Name;
    }

    private static string CleanVersion(string source)
    {
        int pos = source.IndexOf(value: '+');

        return pos != -1
            ? source.Substring(startIndex: 0, length: pos)
            : source;
    }
}
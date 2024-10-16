using Credfeto.Version.Information.Generator.Builders;
using Microsoft.CodeAnalysis;

namespace Credfeto.Version.Information.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class VersionInformationCodeGenerator : ISourceGenerator
{
    private const string CLASS_NAME = "VersionInformation";

    public void Initialize(GeneratorInitializationContext context)
    {
        // Nothing to do here
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key: "build_property.AssemblyNamespace", out string? assemblyNamespace))
        {
            return;
        }

        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key: "build_property.Product", out string? product))
        {
            return;
        }

        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key: "build_property.Version", out string? version))
        {
            version = "1.0.0";
        }

        CodeBuilder cb = new();

        cb.AppendLine("using System");
        cb.AppendBlankLine();
        cb.AppendLine($"namespace {assemblyNamespace}");
        cb.AppendBlankLine();

        using (cb.StartBlock("internal static class VersionInformation"))
        {
            cb.AppendLine($"public const string ProgramVersion = \"{version}\";");
            cb.AppendLine($"public const string Product = \"{product}\";");
        }

        context.AddSource($"{assemblyNamespace}.{CLASS_NAME}.generated.cs", sourceText: cb.Text);
    }
}
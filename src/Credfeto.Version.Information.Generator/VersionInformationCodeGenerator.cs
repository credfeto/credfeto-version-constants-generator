using Credfeto.Version.Information.Generator.Builders;
using Credfeto.Version.Information.Generator.Helpers;
using Microsoft.CodeAnalysis;

namespace Credfeto.Version.Information.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class VersionInformationCodeGenerator : ISourceGenerator
{
    private const string CLASS_NAME = "VersionInformation";
    private readonly string _generatorVersion;
    private readonly ISyntaxContextReceiver _receiver = new SyntaxContextReciever();

    public VersionInformationCodeGenerator()
    {
        this._generatorVersion = this._receiver.GetType()
                                     .Assembly.GetName()
                                     .Version.ToString();
    }

    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        string assemblyNamespace = GetAssemblyNamespace(context);
        string product = GetAssemblyProduct(context: context, assemblyNamespace: assemblyNamespace);
        string version = GetAssemblyVersion(context);

        CodeBuilder source = this.BuildSource(assemblyNamespace: assemblyNamespace, version: version, product: product);

        context.AddSource($"{assemblyNamespace}.{CLASS_NAME}.generated.cs", sourceText: source.Text);
    }

    private CodeBuilder BuildSource(string assemblyNamespace, string version, string product)
    {
        CodeBuilder source = new();

        source.AppendFileHeader()
              .AppendLine("using System;")
              .AppendLine("using System.CodeDom.Compiler;")
              .AppendBlankLine()
              .AppendLine($"namespace {assemblyNamespace};")
              .AppendBlankLine()
              .AppendLine($"[GeneratedCode(tool: \"{typeof(VersionInformationCodeGenerator).FullName}\", version: \"{this._generatorVersion}\")]");

        using (source.StartBlock("internal static class VersionInformation"))
        {
            source.AppendLine($"public const string FileVersion = \"{version}\";");
            source.AppendLine($"public const string Product = \"{product}\";");
        }

        return source;
    }

    private static string GetAssemblyProduct(in GeneratorExecutionContext context, string assemblyNamespace)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key: "build_property.rootnamespace", out string? product))
        {
            product = assemblyNamespace;
        }

        return product;
    }

    private static string GetAssemblyVersion(in GeneratorExecutionContext context)
    {
        return context.Compilation.Assembly.Identity.Version.ToString();
    }

    private static string GetAssemblyNamespace(in GeneratorExecutionContext context)
    {
        return context.Compilation.Assembly.Identity.Name;
    }
}
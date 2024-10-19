using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Credfeto.Version.Information.Generator.Builders;
using Credfeto.Version.Information.Generator.Extensions;
using Credfeto.Version.Information.Generator.Helpers;
using Credfeto.Version.Information.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Credfeto.Version.Information.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class VersionInformationCodeGenerator : IIncrementalGenerator
{
    private const string CLASS_NAME = "VersionInformation";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        HashSet<string> generated = new(StringComparer.Ordinal);

        IncrementalValuesProvider<(NamespaceGeneration? classInfo, ErrorInfo? errorInfo)> namespaces =
            context.SyntaxProvider.CreateSyntaxProvider(predicate: static (n, _) => n is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax, transform: (sc, ct) => GetNamespace(sc, generated, ct ));

        IncrementalValuesProvider<((NamespaceGeneration? classInfo, ErrorInfo? errorInfo) Left, AnalyzerConfigOptionsProvider Right)> withOptions =
            namespaces.Combine(context.AnalyzerConfigOptionsProvider);


        context.RegisterSourceOutput(source: withOptions, action: GenerateVersionInformation);
    }

    private static (NamespaceGeneration? namespaceInfo, ErrorInfo? errorInfo) GetNamespace(in GeneratorSyntaxContext generatorSyntaxContext, HashSet<string> generated, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (generatorSyntaxContext.Node is not NamespaceDeclarationSyntax and not FileScopedNamespaceDeclarationSyntax)
        {
            return (null, InvalidInfo(generatorSyntaxContext));
        }

        Compilation compilation = generatorSyntaxContext.SemanticModel.Compilation;

        AssemblyIdentity assembly = GetAssembly(compilation);

        if(!generated.Add(assembly.Name))
        {
            return (null, null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        ImmutableDictionary<string, string> attributes = ExtractAttributes(compilation.Assembly);
        cancellationToken.ThrowIfCancellationRequested();

        return (new NamespaceGeneration(assembly: assembly, attributes: attributes), null);
    }

    private static ImmutableDictionary<string, string> ExtractAttributes(in IAssemblySymbol ass)
    {
        ImmutableArray<AttributeData> attibuteData = ass.GetAttributes();

        ImmutableDictionary<string, string> attributes = ImmutableDictionary<string, string>.Empty;

        foreach (AttributeData a in attibuteData)
        {
            if (a.AttributeClass is null || a.ConstructorArguments.Length != 1)
            {
                continue;
            }

            string key = a.AttributeClass.Name;

            if (attributes.ContainsKey(key))
            {
                continue;
            }

            object? v = a.ConstructorArguments[0].Value;
            string value = v?.ToString() ?? string.Empty;

            attributes = attributes.Add(key: key, value: value);
        }

        return attributes;
    }

    private static ErrorInfo InvalidInfo(in GeneratorSyntaxContext generatorSyntaxContext)
    {
        return new(generatorSyntaxContext.Node.GetLocation(), new InvalidOperationException("Expected a namespace declaration"));
    }

    private static AssemblyIdentity GetAssembly(Compilation compilation)
    {
        return compilation.Assembly.Identity;
    }

    private static void ReportException(Location location, in SourceProductionContext context, Exception exception)
    {
        context.ReportDiagnostic(diagnostic: Diagnostic.Create(CreateUnhandledExceptionDiagnostic(exception), location: location));
    }

    private static DiagnosticDescriptor CreateUnhandledExceptionDiagnostic(Exception exception)
    {
        return new(id: "VER002",
                   title: "Unhandled Exception",
                   exception.Message + ' ' + exception.StackTrace,
                   category: RuntimeVersionInformation.ToolName,
                   defaultSeverity: DiagnosticSeverity.Error,
                   isEnabledByDefault: true);
    }

    private static void GenerateVersionInformation(SourceProductionContext sourceProductionContext,
                                                   ((NamespaceGeneration? namespaceInfo, ErrorInfo? errorInfo) Left, AnalyzerConfigOptionsProvider Right) item)
    {
        if (item.Left.errorInfo is not null)
        {
            ErrorInfo ei = item.Left.errorInfo.Value;
            ReportException(location: ei.Location, context: sourceProductionContext, exception: ei.Exception);

            return;
        }

        if (item.Left.namespaceInfo is null)
        {
            return;
        }

        GenerateVersionInformation(sourceProductionContext: sourceProductionContext, namespaceInfo: item.Left.namespaceInfo.Value, analyzerConfigOptionsProvider: item.Right);
    }

    private static void GenerateVersionInformation(in SourceProductionContext sourceProductionContext,
                                                   in NamespaceGeneration namespaceInfo,
                                                   AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
    {
        CodeBuilder source = namespaceInfo.BuildSource(analyzerConfigOptionsProvider: analyzerConfigOptionsProvider, out string ns);

        sourceProductionContext.AddSource($"{ns}.{CLASS_NAME}.generated.cs", sourceText: source.Text);
    }
}
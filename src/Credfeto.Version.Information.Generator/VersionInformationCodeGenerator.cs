using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

    private static readonly NamespaceError IgnoreResult = new();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(ExtractNamespaces(context), action: GenerateVersionInformation);
    }

    private static IncrementalValuesProvider<( NamespaceError Left, AnalyzerConfigOptionsProvider Right )> ExtractNamespaces(in IncrementalGeneratorInitializationContext context)
    {
        HashSet<string> assembliesToGenerateVersionInformation = new(StringComparer.Ordinal);

        return context.SyntaxProvider.CreateSyntaxProvider(predicate: static (syntaxNode, _) => syntaxNode is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax,
                                                           transform: (generatorSyntaxContext, cancellationToken) =>
                                                                          GetNamespace(generatorSyntaxContext: generatorSyntaxContext,
                                                                                       generated: assembliesToGenerateVersionInformation,
                                                                                       cancellationToken: cancellationToken))
                      .Combine(context.AnalyzerConfigOptionsProvider);
    }

    private static NamespaceError GetNamespace(in GeneratorSyntaxContext generatorSyntaxContext, HashSet<string> generated, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (generatorSyntaxContext.Node is not NamespaceDeclarationSyntax and not FileScopedNamespaceDeclarationSyntax)
            {
                return IgnoreResult;
            }

            Compilation compilation = generatorSyntaxContext.SemanticModel.Compilation;

            AssemblyIdentity assembly = GetAssembly(compilation);

            if (!generated.Add(assembly.Name))
            {
                return IgnoreResult;
            }

            cancellationToken.ThrowIfCancellationRequested();

            ImmutableDictionary<string, string> attributes = ExtractAttributes(compilation.Assembly);
            cancellationToken.ThrowIfCancellationRequested();

            return new(new NamespaceGeneration(assembly: assembly, attributes: attributes));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return UnhandledException(generatorSyntaxContext: generatorSyntaxContext, exception: exception);
        }
    }

    private static NamespaceError UnhandledException(in GeneratorSyntaxContext generatorSyntaxContext, Exception exception)
    {
        return new(new ErrorInfo(generatorSyntaxContext.Node.GetLocation(), exception: exception));
    }

    private static ImmutableDictionary<string, string> ExtractAttributes(in IAssemblySymbol assemblySymbol)
    {
        // ! Already filtered out the attributes that are not needed
        return assemblySymbol.GetAttributes()
                             .Select(ExtractAttributes)
                             .Where(a => a.HasValue)
                             .Select(a => a!.Value)
                             .Aggregate(seed: ImmutableDictionary<string, string>.Empty,
                                        func: Include);
    }

    private static ImmutableDictionary<string, string> Include(ImmutableDictionary<string, string> result, (string key, string value) element)
    {
        return result.ContainsKey(element.key)
            ? result
            : result.Add(key: element.key, value: element.value);
    }

    private static (string key, string value)? ExtractAttributes(AttributeData a)
    {
        if (a.AttributeClass is null || a.ConstructorArguments.Length != 1)
        {
            return null;
        }

        object? v = a.ConstructorArguments[0].Value;

        return (key: a.AttributeClass.Name, value: v?.ToString() ?? string.Empty);
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
        return new(id: "VER001",
                   title: "Unhandled Exception",
                   exception.Message + ' ' + exception.StackTrace,
                   category: RuntimeVersionInformation.ToolName,
                   defaultSeverity: DiagnosticSeverity.Error,
                   isEnabledByDefault: true);
    }

    private static void GenerateVersionInformation(SourceProductionContext sourceProductionContext, (NamespaceError Left, AnalyzerConfigOptionsProvider Right) item)
    {
        if (item.Left.ErrorInfo is not null)
        {
            ErrorInfo ei = item.Left.ErrorInfo.Value;
            ReportException(location: ei.Location, context: sourceProductionContext, exception: ei.Exception);

            return;
        }

        if (item.Left.NamespaceInfo is null)
        {
            return;
        }

        GenerateVersionInformation(sourceProductionContext: sourceProductionContext, namespaceInfo: item.Left.NamespaceInfo.Value, analyzerConfigOptionsProvider: item.Right);
    }

    private static void GenerateVersionInformation(in SourceProductionContext sourceProductionContext,
                                                   in NamespaceGeneration namespaceInfo,
                                                   AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
    {
        CodeBuilder source = namespaceInfo.BuildSource(analyzerConfigOptionsProvider: analyzerConfigOptionsProvider, out string ns);

        sourceProductionContext.AddSource($"{ns}.{CLASS_NAME}.generated.cs", sourceText: source.Text);
    }
}
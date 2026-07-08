using System;
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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<bool> hasNamespaces = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (syntaxNode, _) =>
                    syntaxNode is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax,
                transform: static (_, _) => true
            )
            .Collect()
            .Select(static (items, _) => !items.IsEmpty);

        IncrementalValueProvider<NamespaceError> assemblyInfo = context.CompilationProvider.Select(
            static (compilation, cancellationToken) => ExtractAssemblyInfo(compilation, cancellationToken)
        );

        IncrementalValueProvider<(NamespaceError Left, AnalyzerConfigOptionsProvider Right)> combined = assemblyInfo
            .Combine(hasNamespaces)
            .Select(static (pair, _) => pair.Right ? pair.Left : new NamespaceError())
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(combined, action: GenerateVersionInformation);
    }

    private static NamespaceError ExtractAssemblyInfo(Compilation compilation, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            AssemblyIdentity assembly = compilation.Assembly.Identity;
            cancellationToken.ThrowIfCancellationRequested();

            ImmutableDictionary<string, string> attributes = ExtractAttributes(compilation.Assembly);
            cancellationToken.ThrowIfCancellationRequested();

            return new(new NamespaceGeneration(assembly: assembly, attributes: attributes));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(new ErrorInfo(Location.None, exception: exception));
        }
    }

    private static ImmutableDictionary<string, string> ExtractAttributes(in IAssemblySymbol assemblySymbol)
    {
        // ! Already filtered out the attributes that are not needed
        return assemblySymbol
            .GetAttributes()
            .Select(ExtractAttributes)
            .Where(a => a.HasValue)
            .Select(a => a!.Value)
            .Aggregate(seed: ImmutableDictionary<string, string>.Empty, func: Include);
    }

    private static ImmutableDictionary<string, string> Include(
        ImmutableDictionary<string, string> result,
        (string key, string value) element
    )
    {
        return result.ContainsKey(element.key) ? result : result.Add(key: element.key, value: element.value);
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

    private static void ReportException(Location location, in SourceProductionContext context, Exception exception)
    {
        context.ReportDiagnostic(
            diagnostic: Diagnostic.Create(CreateUnhandledExceptionDiagnostic(exception), location: location)
        );
    }

    private static DiagnosticDescriptor CreateUnhandledExceptionDiagnostic(Exception exception)
    {
        return new(
            id: "VER001",
            title: "Unhandled Exception",
            exception.Message + ' ' + exception.StackTrace,
            category: RuntimeVersionInformation.ToolName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
    }

    private static void GenerateVersionInformation(
        SourceProductionContext sourceProductionContext,
        (NamespaceError Left, AnalyzerConfigOptionsProvider Right) item
    )
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

        GenerateVersionInformation(
            sourceProductionContext: sourceProductionContext,
            namespaceInfo: item.Left.NamespaceInfo.Value,
            analyzerConfigOptionsProvider: item.Right
        );
    }

    private static void GenerateVersionInformation(
        in SourceProductionContext sourceProductionContext,
        in NamespaceGeneration namespaceInfo,
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider
    )
    {
        CodeBuilder source = namespaceInfo.BuildSource(
            analyzerConfigOptionsProvider: analyzerConfigOptionsProvider,
            out string ns
        );

        sourceProductionContext.AddSource($"{ns}.{CLASS_NAME}.generated.cs", sourceText: source.Text);
    }
}

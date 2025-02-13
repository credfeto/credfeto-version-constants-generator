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

    private static readonly (
        NamespaceGeneration? namespaceInfo,
        ErrorInfo? errorInfo
    ) IgnoreResult = (namespaceInfo: null, errorInfo: null);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            ExtractNamespaces(context),
            action: GenerateVersionInformation
        );
    }

    private static IncrementalValuesProvider<(
        (NamespaceGeneration? namespaceInfo, ErrorInfo? errorInfo) Left,
        AnalyzerConfigOptionsProvider Right
    )> ExtractNamespaces(in IncrementalGeneratorInitializationContext context)
    {
        HashSet<string> assembliesToGenerateVersionInformation = new(StringComparer.Ordinal);

        IncrementalValuesProvider<(
            NamespaceGeneration? namespaceInfo,
            ErrorInfo? errorInfo
        )> namespaces = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (n, _) =>
                n is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax,
            transform: (sc, ct) =>
                GetNamespace(
                    generatorSyntaxContext: sc,
                    generated: assembliesToGenerateVersionInformation,
                    cancellationToken: ct
                )
        );

        IncrementalValuesProvider<(
            (NamespaceGeneration? namespaceInfo, ErrorInfo? errorInfo) Left,
            AnalyzerConfigOptionsProvider Right
        )> withOptions = namespaces.Combine(context.AnalyzerConfigOptionsProvider);

        return withOptions;
    }

    private static (NamespaceGeneration? namespaceInfo, ErrorInfo? errorInfo) GetNamespace(
        in GeneratorSyntaxContext generatorSyntaxContext,
        HashSet<string> generated,
        CancellationToken cancellationToken
    )
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (
                generatorSyntaxContext.Node
                is not NamespaceDeclarationSyntax
                    and not FileScopedNamespaceDeclarationSyntax
            )
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

            ImmutableDictionary<string, string> attributes = ExtractAttributes(
                compilation.Assembly
            );
            cancellationToken.ThrowIfCancellationRequested();

            return (new NamespaceGeneration(assembly: assembly, attributes: attributes), null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return UnhandledException(
                generatorSyntaxContext: generatorSyntaxContext,
                exception: exception
            );
        }
    }

    private static (NamespaceGeneration? namespaceInfo, ErrorInfo? errorInfo) UnhandledException(
        in GeneratorSyntaxContext generatorSyntaxContext,
        Exception exception
    )
    {
        return (
            namespaceInfo: null,
            errorInfo: new ErrorInfo(
                generatorSyntaxContext.Node.GetLocation(),
                exception: exception
            )
        );
    }

    private static ImmutableDictionary<string, string> ExtractAttributes(
        in IAssemblySymbol assemblySymbol
    )
    {
        ImmutableDictionary<string, string> attributes = ImmutableDictionary<string, string>.Empty;

        // ! Already filtered out the attributes that are not needed
        foreach (
            (string key, string value) in assemblySymbol
                .GetAttributes()
                .Select(ExtractAttributes)
                .Where(a => a.HasValue)
                .Select(a => a!.Value)
                .Where(item => !attributes.ContainsKey(item.key))
        )
        {
            attributes = attributes.Add(key: key, value: value);
        }

        return attributes;
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

    private static void ReportException(
        Location location,
        in SourceProductionContext context,
        Exception exception
    )
    {
        context.ReportDiagnostic(
            diagnostic: Diagnostic.Create(
                CreateUnhandledExceptionDiagnostic(exception),
                location: location
            )
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
        (
            (NamespaceGeneration? namespaceInfo, ErrorInfo? errorInfo) Left,
            AnalyzerConfigOptionsProvider Right
        ) item
    )
    {
        if (item.Left.errorInfo is not null)
        {
            ErrorInfo ei = item.Left.errorInfo.Value;
            ReportException(
                location: ei.Location,
                context: sourceProductionContext,
                exception: ei.Exception
            );

            return;
        }

        if (item.Left.namespaceInfo is null)
        {
            return;
        }

        GenerateVersionInformation(
            sourceProductionContext: sourceProductionContext,
            namespaceInfo: item.Left.namespaceInfo.Value,
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

        sourceProductionContext.AddSource(
            $"{ns}.{CLASS_NAME}.generated.cs",
            sourceText: source.Text
        );
    }
}

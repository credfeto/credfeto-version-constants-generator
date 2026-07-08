using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Credfeto.Version.Information.Generator.Tests.TestSupport;
using FunFair.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Credfeto.Version.Information.Generator.Tests;

public sealed class VersionInformationCodeGeneratorTests : TestBase
{
    private static GeneratorDriverRunResult RunGenerator(
        string source,
        string assemblyName = "TestAssembly",
        IReadOnlyDictionary<string, string>? globalOptions = null
    )
    {
        return RunGeneratorWithMultipleTrees(assemblyName: assemblyName, globalOptions: globalOptions, sources: source);
    }

    private static GeneratorDriverRunResult RunGeneratorWithMultipleTrees(
        string assemblyName,
        IReadOnlyDictionary<string, string>? globalOptions,
        params string[] sources
    )
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        List<SyntaxTree> trees = [];

        foreach (string source in sources)
        {
            trees.Add(CSharpSyntaxTree.ParseText(text: source, cancellationToken: cancellationToken));
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: trees,
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new VersionInformationCodeGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: null,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(globalOptions)
        );

        driver = driver.RunGenerators(compilation: compilation, cancellationToken: cancellationToken);

        return driver.GetRunResult();
    }

    private static IReadOnlyList<MetadataReference> GetReferences()
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<MetadataReference> refs = [];

        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) && seen.Add(assembly.Location))
            {
                refs.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        return refs;
    }

    [Fact]
    public void GeneratorStripsGitHashFromVersion()
    {
        const string SOURCE = """
            using System.Reflection;
            [assembly: AssemblyInformationalVersion("1.2.3.4+abc123")]
            namespace TestAssembly;
            public class TestClass { }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "TestAssembly");

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);

        string generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("\"1.2.3.4\"", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("+abc123", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorPreservesVersionWithoutGitHash()
    {
        const string SOURCE = """
            using System.Reflection;
            [assembly: AssemblyInformationalVersion("1.2.3.4")]
            namespace TestAssembly;
            public class TestClass { }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "TestAssembly");

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);

        string generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("\"1.2.3.4\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void RootNamespaceOptionOverridesAssemblyName()
    {
        const string SOURCE = """
            namespace TestAssembly;
            public class TestClass { }
            """;

        Dictionary<string, string> options = new(StringComparer.Ordinal)
        {
            ["build_property.rootnamespace"] = "MyOverriddenNamespace",
        };

        GeneratorDriverRunResult result = RunGenerator(
            source: SOURCE,
            assemblyName: "TestAssembly",
            globalOptions: options
        );

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);

        string generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("namespace MyOverriddenNamespace", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AbsentRootNamespaceFallsBackToAssemblyName()
    {
        const string SOURCE = """
            namespace TestAssembly;
            public class TestClass { }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "TestAssembly");

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);

        string generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("namespace TestAssembly", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AssemblyTitleAffectsProductConstant()
    {
        const string SOURCE = """
            using System.Reflection;
            [assembly: AssemblyTitle("My Product")]
            namespace TestAssembly;
            public class TestClass { }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "TestAssembly");

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);

        string generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("\"My Product\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AbsentAssemblyTitleFallsBackToNamespace()
    {
        const string SOURCE = """
            namespace TestAssembly;
            public class TestClass { }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "TestAssembly");

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);

        string generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("Product = \"TestAssembly\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AssemblyCompanyAndCopyrightAppearInGeneratedCode()
    {
        const string SOURCE = """
            using System.Reflection;
            [assembly: AssemblyCompany("Acme Corp")]
            [assembly: AssemblyCopyright("Copyright 2024 Acme Corp")]
            namespace TestAssembly;
            public class TestClass { }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "TestAssembly");

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);

        string generated = result.Results[0].GeneratedSources[0].SourceText.ToString();
        Assert.Contains("\"Acme Corp\"", generated, StringComparison.Ordinal);
        Assert.Contains("\"Copyright 2024 Acme Corp\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateNamespacesProduceOnlyOneGeneratedFile()
    {
        const string SOURCE_1 = """
            namespace Ns1;
            public class C1 { }
            """;

        const string SOURCE_2 = """
            namespace Ns2;
            public class C2 { }
            """;

        GeneratorDriverRunResult result = RunGeneratorWithMultipleTrees(
            assemblyName: "TestAssembly",
            globalOptions: null,
            SOURCE_1,
            SOURCE_2
        );

        Assert.NotEmpty(result.Results);

        int totalGeneratedSources = 0;

        foreach (GeneratorRunResult generatorResult in result.Results)
        {
            totalGeneratedSources += generatorResult.GeneratedSources.Length;
        }

        Assert.Equal(1, totalGeneratedSources);
    }

    [Fact]
    public void NoNamespaceSourceProducesNoGeneratedFiles()
    {
        const string SOURCE = "public class GlobalClass { }";

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "TestAssembly");

        int totalGeneratedSources = 0;

        foreach (GeneratorRunResult generatorResult in result.Results)
        {
            totalGeneratedSources += generatorResult.GeneratedSources.Length;
        }

        Assert.Equal(0, totalGeneratedSources);
    }

    [Fact]
    public void GeneratedSourceHasExpectedStructure()
    {
        const string SOURCE = """
            using System.Reflection;
            [assembly: AssemblyInformationalVersion("2.0.0.0")]
            namespace TestAssembly;
            public class TestClass { }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "TestAssembly");

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);

        SourceText sourceText = result.Results[0].GeneratedSources[0].SourceText;
        string generated = sourceText.ToString();

        Assert.Contains(
            "//------------------------------------------------------------------------------",
            generated,
            StringComparison.Ordinal
        );
        Assert.Contains("using System;", generated, StringComparison.Ordinal);
        Assert.Contains("using System.CodeDom.Compiler;", generated, StringComparison.Ordinal);
        Assert.Contains("[GeneratedCode(", generated, StringComparison.Ordinal);
        Assert.Contains("internal static class VersionInformation", generated, StringComparison.Ordinal);
        Assert.Contains("public const string Version", generated, StringComparison.Ordinal);
        Assert.Contains("public const string Product", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void FileScopedNamespaceTriggerGeneratesCode()
    {
        const string SOURCE = """
            namespace FileScopedNs;
            public class Foo { }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "FileScopedNs");

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);
    }

    [Fact]
    public void BlockNamespaceTriggerGeneratesCode()
    {
        const string SOURCE = """
            namespace BlockNs
            {
                public class Foo { }
            }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "BlockNs");

        Assert.NotEmpty(result.Results);
        Assert.NotEmpty(result.Results[0].GeneratedSources);
    }

    [Fact]
    public void GeneratedSourceHasNoDiagnosticsForValidInput()
    {
        const string SOURCE = """
            namespace TestAssembly;
            public class TestClass { }
            """;

        GeneratorDriverRunResult result = RunGenerator(source: SOURCE, assemblyName: "TestAssembly");

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void GeneratorStillProducesSourceAfterIncrementalRebuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        const string SOURCE_1 = """
            namespace TestAssembly;
            public class C1 { }
            """;

        const string SOURCE_2 = """
            namespace TestAssembly;
            public class C2 { }
            """;

        CSharpCompilation compilation1 = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(text: SOURCE_1, cancellationToken: cancellationToken)],
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new VersionInformationCodeGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: null,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(null)
        );

        driver = driver.RunGenerators(compilation: compilation1, cancellationToken: cancellationToken);
        GeneratorDriverRunResult result1 = driver.GetRunResult();

        int sources1 = 0;

        foreach (GeneratorRunResult r in result1.Results)
        {
            sources1 += r.GeneratedSources.Length;
        }

        Assert.Equal(1, sources1);

        CSharpCompilation compilation2 = compilation1.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(text: SOURCE_2, cancellationToken: cancellationToken)
        );

        driver = driver.RunGenerators(compilation: compilation2, cancellationToken: cancellationToken);
        GeneratorDriverRunResult result2 = driver.GetRunResult();

        int sources2 = 0;

        foreach (GeneratorRunResult r in result2.Results)
        {
            sources2 += r.GeneratedSources.Length;
        }

        Assert.Equal(1, sources2);
    }

    private static readonly string[] AllTrackingNames =
    [
        VersionInformationCodeGenerator.TRACKING_NAME_HAS_NAMESPACES,
        VersionInformationCodeGenerator.TRACKING_NAME_ASSEMBLY_INFO,
        VersionInformationCodeGenerator.TRACKING_NAME_ROOT_NAMESPACE,
        VersionInformationCodeGenerator.TRACKING_NAME_COMBINED,
    ];

    private static CSharpCompilation CreateSingleFileCompilation(string source, in CancellationToken cancellationToken)
    {
        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(text: source, cancellationToken: cancellationToken)],
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    [Fact]
    public void PipelineStepsAreCachedOnSemanticallyIrrelevantRerun()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        const string SOURCE_1 = """
            namespace TestAssembly;
            public class C1 { }
            """;

        const string SOURCE_1_WITH_EXTRA_WHITESPACE = """
            namespace TestAssembly;


            public class C1 { }
            """;

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new VersionInformationCodeGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: null,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(null),
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );

        driver = driver.RunGenerators(
            compilation: CreateSingleFileCompilation(source: SOURCE_1, cancellationToken: cancellationToken),
            cancellationToken: cancellationToken
        );

        driver = driver.RunGenerators(
            compilation: CreateSingleFileCompilation(
                source: SOURCE_1_WITH_EXTRA_WHITESPACE,
                cancellationToken: cancellationToken
            ),
            cancellationToken: cancellationToken
        );

        GeneratorDriverRunResult result = driver.GetRunResult();

        Assert.NotEmpty(result.Results);

        foreach (string trackingName in AllTrackingNames)
        {
            AssertStepsCachedOrUnchanged(result.Results[0].TrackedSteps[trackingName]);
        }
    }

    private static void AssertStepsCachedOrUnchanged(in ImmutableArray<IncrementalGeneratorRunStep> steps)
    {
        Assert.NotEmpty(steps);

        foreach (IncrementalGeneratorRunStep step in steps)
        {
            foreach ((_, IncrementalStepRunReason reason) in step.Outputs)
            {
                Assert.True(
                    reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    "Expected step output reason to be Cached or Unchanged"
                );
            }
        }
    }
}

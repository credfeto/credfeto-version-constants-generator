using System;
using System.Collections.Generic;
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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [CSharpSyntaxTree.ParseText(text: source, cancellationToken: cancellationToken)],
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
}

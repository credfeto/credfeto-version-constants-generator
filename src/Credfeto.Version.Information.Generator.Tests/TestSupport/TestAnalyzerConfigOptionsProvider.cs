using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Credfeto.Version.Information.Generator.Tests.TestSupport;

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly ConfigOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string>? globalOptions = null)
    {
        this._globalOptions = new ConfigOptions(
            globalOptions ?? new Dictionary<string, string>(StringComparer.Ordinal)
        );
    }

    public override AnalyzerConfigOptions GlobalOptions => this._globalOptions;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return this._globalOptions;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return this._globalOptions;
    }

    private sealed class ConfigOptions : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _options;

        public ConfigOptions(IReadOnlyDictionary<string, string> options)
        {
            this._options = options;
        }

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            return this._options.TryGetValue(key, out value);
        }
    }
}

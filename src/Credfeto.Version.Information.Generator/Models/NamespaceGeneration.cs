using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Credfeto.Version.Information.Generator.Models;

[DebuggerDisplay("{Namespace}")]
public readonly record struct NamespaceGeneration
{
    public NamespaceGeneration(AssemblyIdentity assembly, ImmutableDictionary<string, string> attributes)
    {
        this.Assembly = assembly;
        this.Attributes = attributes;
    }

    public string Namespace => this.Assembly.Name;

    public AssemblyIdentity Assembly { get; }

    public ImmutableDictionary<string, string> Attributes { get; }

    public bool Equals(NamespaceGeneration other)
    {
        if (!this.Assembly.Equals(other.Assembly))
        {
            return false;
        }

        if (this.Attributes.Count != other.Attributes.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> kvp in this.Attributes)
        {
            if (
                !other.Attributes.TryGetValue(kvp.Key, out string? value)
                || !string.Equals(value, kvp.Value, StringComparison.Ordinal)
            )
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        int h = this.Assembly.GetHashCode();

        foreach (KeyValuePair<string, string> kvp in this.Attributes)
        {
            h ^= StringComparer.Ordinal.GetHashCode(kvp.Key) ^ StringComparer.Ordinal.GetHashCode(kvp.Value);
        }

        return h;
    }
}

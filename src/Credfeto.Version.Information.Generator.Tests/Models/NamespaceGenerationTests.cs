using System;
using System.Collections.Immutable;
using Credfeto.Version.Information.Generator.Models;
using FunFair.Test.Common;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Credfeto.Version.Information.Generator.Tests.Models;

public sealed class NamespaceGenerationTests : TestBase
{
    private static readonly AssemblyIdentity Identity = new(name: "TestAssembly", version: new(1, 2, 3, 4));

    private static NamespaceGeneration Create(params (string Key, string Value)[] attributes)
    {
        ImmutableDictionary<string, string>.Builder builder = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal
        );

        foreach ((string key, string value) in attributes)
        {
            builder.Add(key: key, value: value);
        }

        return new(assembly: Identity, attributes: builder.ToImmutable());
    }

    [Fact]
    public void NamespaceIsAssemblyName()
    {
        NamespaceGeneration generation = Create();

        Assert.Equal(expected: "TestAssembly", actual: generation.Namespace);
    }

    [Fact]
    public void InstancesWithSameAssemblyAndAttributesAreEqual()
    {
        NamespaceGeneration first = Create(("A", "1"), ("B", "2"));
        NamespaceGeneration second = Create(("B", "2"), ("A", "1"));

        Assert.Equal(expected: first, actual: second);
        Assert.Equal(expected: first.GetHashCode(), actual: second.GetHashCode());
    }

    [Fact]
    public void InstancesWithDifferentAssembliesAreNotEqual()
    {
        NamespaceGeneration first = Create(("A", "1"));
        NamespaceGeneration second = new(
            assembly: new(name: "OtherAssembly", version: new(1, 2, 3, 4)),
            attributes: first.Attributes
        );

        Assert.NotEqual(expected: first, actual: second);
    }

    [Fact]
    public void InstancesWithDifferentAttributeCountsAreNotEqual()
    {
        NamespaceGeneration first = Create(("A", "1"));
        NamespaceGeneration second = Create(("A", "1"), ("B", "2"));

        Assert.NotEqual(expected: first, actual: second);
    }

    [Fact]
    public void InstancesWithDifferentAttributeValuesAreNotEqual()
    {
        NamespaceGeneration first = Create(("A", "1"));
        NamespaceGeneration second = Create(("A", "2"));

        Assert.NotEqual(expected: first, actual: second);
    }

    [Fact]
    public void InstancesWithDifferentAttributeKeysAreNotEqual()
    {
        NamespaceGeneration first = Create(("A", "1"));
        NamespaceGeneration second = Create(("B", "1"));

        Assert.NotEqual(expected: first, actual: second);
    }
}

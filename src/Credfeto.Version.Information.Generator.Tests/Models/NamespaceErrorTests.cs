using System;
using Credfeto.Version.Information.Generator.Models;
using FunFair.Test.Common;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Credfeto.Version.Information.Generator.Tests.Models;

public sealed class NamespaceErrorTests : TestBase
{
    [Fact]
    public void DefaultConstructorHasNullNamespaceInfoAndErrorInfo()
    {
        NamespaceError namespaceError = new();
        Assert.Null(namespaceError.NamespaceInfo);
        Assert.Null(namespaceError.ErrorInfo);
    }

    [Fact]
    public void ConstructorWithNullNamespaceGenerationHasNullNamespaceInfo()
    {
        NamespaceError namespaceError = new(namespaceInfo: null);
        Assert.Null(namespaceError.NamespaceInfo);
        Assert.Null(namespaceError.ErrorInfo);
    }

    [Fact]
    public void ConstructorWithErrorInfoSetsErrorInfoAndNullNamespaceInfo()
    {
        ErrorInfo errorInfo = new(location: Location.None, exception: new InvalidOperationException("test"));
        NamespaceError namespaceError = new(errorInfo: errorInfo);
        Assert.Null(namespaceError.NamespaceInfo);
        Assert.NotNull(namespaceError.ErrorInfo);
        Assert.Equal(errorInfo, namespaceError.ErrorInfo.Value);
    }

    [Fact]
    public void ConstructorWithNullErrorInfoHasNullErrorInfo()
    {
        NamespaceError namespaceError = new(errorInfo: null);
        Assert.Null(namespaceError.NamespaceInfo);
        Assert.Null(namespaceError.ErrorInfo);
    }
}

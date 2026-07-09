using System;
using Credfeto.Version.Information.Generator.Models;
using FunFair.Test.Common;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Credfeto.Version.Information.Generator.Tests.Models;

public sealed class ErrorInfoTests : TestBase
{
    [Fact]
    public void LocationIsStoredCorrectly()
    {
        Exception exception = new InvalidOperationException("test error");
        ErrorInfo errorInfo = new(location: Location.None, exception: exception);
        Assert.Equal(Location.None.ToString(), errorInfo.LocationString);
    }

    [Fact]
    public void ExceptionMessageIsStoredCorrectly()
    {
        Exception exception = new InvalidOperationException("test error");
        ErrorInfo errorInfo = new(location: Location.None, exception: exception);
        Assert.Equal(exception.Message, errorInfo.ExceptionMessage);
    }

    [Fact]
    public void ExceptionMessageIsPreserved()
    {
        const string MESSAGE = "specific error message";
        Exception exception = new InvalidOperationException(MESSAGE);
        ErrorInfo errorInfo = new(location: Location.None, exception: exception);
        Assert.Equal(MESSAGE, errorInfo.ExceptionMessage);
    }

    [Fact]
    public void TwoErrorInfosWithSameValuesAreEqual()
    {
        Exception exception = new InvalidOperationException("test");
        ErrorInfo first = new(location: Location.None, exception: exception);
        ErrorInfo second = new(location: Location.None, exception: exception);
        Assert.Equal(first, second);
    }

    [Fact]
    public void TwoErrorInfosWithDifferentExceptionsAreNotEqual()
    {
        ErrorInfo first = new(location: Location.None, exception: new InvalidOperationException("first"));
        ErrorInfo second = new(location: Location.None, exception: new InvalidOperationException("second"));
        Assert.NotEqual(first, second);
    }
}

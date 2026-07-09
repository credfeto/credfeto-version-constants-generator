using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Credfeto.Version.Information.Generator.Models;

[DebuggerDisplay("{LocationString} {ExceptionMessage}")]
public readonly record struct ErrorInfo
{
    public ErrorInfo(Location location, Exception exception)
    {
        this.LocationString = location.ToString();
        this.ExceptionMessage = exception.Message;
        this.ExceptionStackTrace = exception.StackTrace;
    }

    public string LocationString { get; }

    public string ExceptionMessage { get; }

    public string? ExceptionStackTrace { get; }
}

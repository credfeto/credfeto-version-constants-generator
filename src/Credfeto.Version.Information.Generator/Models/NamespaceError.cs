using System.Diagnostics;

namespace Credfeto.Version.Information.Generator.Models;

[DebuggerDisplay("{NamespaceInfo} {ErrorInfo}")]
internal readonly record struct NamespaceError
{
    public NamespaceError()
        : this(namespaceInfo: null, errorInfo: null)
    {

    }

    public NamespaceError(NamespaceGeneration? namespaceInfo)
        : this(namespaceInfo: namespaceInfo, errorInfo: null)
    {

    }

    public NamespaceError(ErrorInfo? errorInfo)
        : this(namespaceInfo: null, errorInfo: errorInfo)
    {

    }

    private NamespaceError(NamespaceGeneration? namespaceInfo, ErrorInfo? errorInfo)
    {
        this.NamespaceInfo = namespaceInfo;
        this.ErrorInfo = errorInfo;
    }

    public NamespaceGeneration? NamespaceInfo { get; }

    public ErrorInfo? ErrorInfo { get; }
}
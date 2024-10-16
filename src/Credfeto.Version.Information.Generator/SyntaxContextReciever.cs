using Microsoft.CodeAnalysis;

namespace Credfeto.Version.Information.Generator;

internal sealed class SyntaxContextReciever : ISyntaxContextReceiver
{
    public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
    {
        // We are not interested in any syntax nodes
    }
}
using System.Reflection;

namespace Credfeto.Version.Information.Generator.Helpers;

internal static class RuntimeVersionInformation
{
    private static readonly AssemblyName AssemblyName = typeof(VersionInformationCodeGenerator).Assembly.GetName();

    public static string ToolName { get; } = AssemblyName.Name;

    public static string GeneratorVersion { get; } = AssemblyName.Version.ToString();
}
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Version.Information.Example.Tests.OtherNameSpace;

public sealed class AlternateNamespaceTest : LoggingTestBase
{
    public AlternateNamespaceTest(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void Version()
    {
        this.Output.WriteLine($"Version: {VersionInformation.Version}");
    }

    [Fact]
    public void Product()
    {
        this.Output.WriteLine($"Product: {VersionInformation.Product}");
    }
}

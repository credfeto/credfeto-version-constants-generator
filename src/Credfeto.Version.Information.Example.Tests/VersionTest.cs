using FunFair.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.Version.Information.Example.Tests;

public sealed class VersionTest : LoggingTestBase
{
    public VersionTest(ITestOutputHelper output)
        : base(output)
    {
    }

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
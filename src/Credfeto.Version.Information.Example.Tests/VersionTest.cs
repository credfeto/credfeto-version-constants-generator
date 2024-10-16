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
    public void FileVersion()
    {
        this.Output.WriteLine($"Version: {VersionInformation.FileVersion}");
    }

    [Fact]
    public void Product()
    {
        this.Output.WriteLine($"Product: {VersionInformation.Product}");
    }
}
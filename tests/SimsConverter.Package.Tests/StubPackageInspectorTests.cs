using System.Text;
using SimsConverter.Domain.Enums;
using SimsConverter.Package.Services;

namespace SimsConverter.Package.Tests;

public class StubPackageInspectorTests
{
    [Fact]
    public void InspectHeader_GivenDbpfHeader_ReturnsUnknownGameVersion()
    {
        var inspector = new StubPackageInspector();
        byte[] header = Encoding.ASCII.GetBytes("DBPF_HEADER_TEST_BYTES");

        var summary = inspector.InspectHeader(header);

        summary.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        summary.MajorVersion.Should().Be("2");
        summary.IndexEntryCount.Should().Be(1);
    }

    [Fact]
    public void InspectHeader_GivenInvalidHeader_ReturnsUnknownVersion()
    {
        var inspector = new StubPackageInspector();
        byte[] header = Encoding.ASCII.GetBytes("INVALID_HEADER");

        var summary = inspector.InspectHeader(header);

        summary.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        summary.MajorVersion.Should().Be("0");
        summary.IndexEntryCount.Should().Be(0);
    }
}

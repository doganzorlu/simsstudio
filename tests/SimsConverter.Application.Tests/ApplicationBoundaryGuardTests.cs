using System;
using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Application.Tests;

public class ApplicationBoundaryGuardTests
{
    [Fact]
    public void ApplicationSource_ContainsNoProhibitedLowLevelBinaryOrFileReadCode()
    {
        // Find src/SimsConverter.Application directory relative to test execution location
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var solutionDir = FindSolutionDirectory(baseDir);
        var appProjectDir = Path.Combine(solutionDir, "src", "SimsConverter.Application");

        Directory.Exists(appProjectDir).Should().BeTrue($"Application project directory must exist at {appProjectDir}");

        var csFiles = Directory.GetFiles(appProjectDir, "*.cs", SearchOption.AllDirectories);
        csFiles.Should().NotBeEmpty("Application project must contain C# source files.");

        var prohibitedTokens = new[]
        {
            "System.Buffers.Binary",
            "BinaryPrimitives",
            "File.ReadAllBytes",
            "AsSpan(",
            ".Slice("
        };

        var violations = new System.Collections.Generic.List<string>();

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var token in prohibitedTokens)
            {
                if (content.Contains(token))
                {
                    violations.Add($"File '{Path.GetFileName(file)}' contains prohibited token '{token}'.");
                }
            }
        }

        violations.Should().BeEmpty("SimsConverter.Application layer must remain free of low-level binary parsing and direct file byte reading.");
    }

    private static string FindSolutionDirectory(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SimsConverter.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"Could not locate SimsConverter.sln from {startDir}.");
    }
}

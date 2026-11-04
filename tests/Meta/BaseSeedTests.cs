using TypeModel.Enums;

namespace Mielia.Tests.Meta;

/// <summary>
/// The resident-JIT base seed has a Mielia source. A seed that is not Mielia (a <c>drop</c>, sized integer names)
/// fails to parse or analyze, and then no Mielia run gets a resident base.
/// </summary>
public sealed class BaseSeedTests
{
    /// <summary>The seed builds in Mielia with no error and no warning, through the driver a program goes through.</summary>
    [Fact]
    public void BaseSeed_Builds_WithoutDiagnostics_InMielia()
    {
        (string source, string fileName) = Builder.Execution.BaseObjectCache.SeedFor(language: Language.Mielia);
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: "sf_seed_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path: dir);
        TextWriter savedOut = Console.Out;
        TextWriter savedErr = Console.Error;
        var captured = new StringWriter();
        try
        {
            string file = Path.Combine(path1: dir, path2: fileName);
            File.WriteAllText(path: file, contents: source);
            Console.SetOut(newOut: captured);
            Console.SetError(newError: captured);
            int rc = Builder.Execution.Program.CompileEntryToIrForTests(entryFile: file, ir: out _);
            Assert.True(condition: rc == 0, userMessage: captured.ToString());
        }
        finally
        {
            Console.SetOut(newOut: savedOut);
            Console.SetError(newError: savedErr);
            Directory.Delete(path: dir, recursive: true);
        }

        Assert.DoesNotContain(expectedSubstring: "warning[", actualString: captured.ToString());
        Assert.DoesNotContain(expectedSubstring: "error[", actualString: captured.ToString());
    }
}

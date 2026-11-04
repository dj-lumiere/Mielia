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
        // The seed build's own diagnostics, not the shared console: tests running in parallel write their
        // diagnostics to the console too.
        List<(string Severity, string Header)> diagnostics = Builder.Diagnostics.DiagnosticRenderer.BeginCapture();
        int rc;
        try
        {
            string file = Path.Combine(path1: dir, path2: fileName);
            File.WriteAllText(path: file, contents: source);
            rc = Builder.Execution.Program.CompileEntryToIrForTests(entryFile: file, ir: out _);
        }
        finally
        {
            Builder.Diagnostics.DiagnosticRenderer.EndCapture();
            Directory.Delete(path: dir, recursive: true);
        }

        string reported = string.Join(separator: "\n", values: diagnostics.Select(selector: d => d.Header));
        Assert.True(condition: rc == 0, userMessage: reported);
        Assert.DoesNotContain(collection: diagnostics, filter: d => d.Severity is "error" or "warning");
    }
}

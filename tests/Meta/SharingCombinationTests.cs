using System.Diagnostics;

namespace Mielia.Tests.Meta;

/// <summary>
/// The sharing combination programs (<c>tests/SharingCombos</c>): a Work and the main task update a count through
/// a handle stored into something they share, over every store site, destination and stored value, built in debug
/// mode with ThreadSanitizer on and the sharing check on. Heavy (about 170 builds), so it runs only when the
/// environment variable <c>MI_SHARING_COMBOS</c> names a scratch folder to build in.
/// </summary>
public sealed class SharingCombinationTests
{
    [Fact]
    public void EveryCombination_CountsRight()
    {
        string? work = Environment.GetEnvironmentVariable(variable: "MI_SHARING_COMBOS");
        if (string.IsNullOrWhiteSpace(value: work))
        {
            return;
        }

        string testsDir = Path.GetFullPath(path: Path.Combine(path1: AppContext.BaseDirectory, path2: "..", path3: "..", path4: ".."));
        string script = Path.Combine(path1: testsDir, path2: "SharingCombos", path3: "run.sh");
        using Process process = Process.Start(startInfo: new ProcessStartInfo("bash", $"\"{script}\" \"{work}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        })!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        string[] failed = output.Split(separator: '\n')
                                .Where(predicate: l => l.Split(separator: ' ')[0] is "BUILD" or "BAD" or "TSAN" or "SHARE" or "TIMEOUT" or "CRASH")
                                .ToArray();
        Assert.True(condition: failed.Length == 0, userMessage: "Failing sharing combinations:\n" + string.Join(separator: "\n", values: failed));
    }
}

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Mielia.Tests.Meta;

/// <summary>
/// A program that uses Work checks every entity access, a program without Work doesn't: the emitted IR of
/// <c>tests/Fixtures/TaskChecks/no_work.mie</c> has no access lock and no promotion, the IR of <c>with_work.mie</c>
/// (the same program with one Work) has them.
/// </summary>
public sealed class TaskCheckTests
{
    private static readonly string RepoRoot = LocateRepoRoot();

    private static readonly Regex LockUse = new(pattern: @"^\s*(call|define)\b.*(lock_enter|lock_exit)", options: RegexOptions.Multiline);

    private static readonly Regex PromoteUse = new(pattern: @"^\s*(call|define)\b.*(\.promote\(\)|ingrid_roam_promote)", options: RegexOptions.Multiline);

    [Fact]
    public void ProgramWithoutWork_HasNoAccessLockAndNoPromotion()
    {
        string ir = Emit(fixture: "no_work.mie");

        Assert.DoesNotMatch(expectedRegex: LockUse, actualString: ir);
        Assert.DoesNotMatch(expectedRegex: PromoteUse, actualString: ir);
    }

    [Fact]
    public void ProgramWithWork_KeepsAccessLockAndPromotion()
    {
        string ir = Emit(fixture: "with_work.mie");

        Assert.Matches(expectedRegex: LockUse, actualString: ir);
        Assert.Matches(expectedRegex: PromoteUse, actualString: ir);
    }

    private static string Emit(string fixture)
    {
        string source = Path.Combine(paths: [RepoRoot, "tests", "Fixtures", "TaskChecks", fixture]);
        string output = Path.Combine(path1: Path.GetTempPath(), path2: $"task_checks_{Guid.NewGuid():N}.ll");
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { TestHelpers.ToolchainDll(sourcePath: source), "codegen", source, output },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot
        };
        using var p = Process.Start(startInfo: psi)!;
        Task<string> outTask = p.StandardOutput.ReadToEndAsync();
        Task<string> errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(milliseconds: TestHelpers.ChildTimeoutMs(baseMs: 300_000)))
        {
            try { p.Kill(entireProcessTree: true); }
            catch
            {
                /* best effort */
            }

            Assert.Fail(message: $"{fixture} did not finish.");
        }

        Assert.True(condition: p.ExitCode == 0 && File.Exists(path: output),
            userMessage: $"codegen failed\n--- stdout ---\n{outTask.Result}\n--- stderr ---\n{errTask.Result}");
        try
        {
            return File.ReadAllText(path: output);
        }
        finally
        {
            File.Delete(path: output);
        }
    }

    private static string LocateRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(value: dir))
        {
            if (File.Exists(path: Path.Combine(path1: dir, path2: "Mielia.csproj")))
            {
                return dir;
            }

            string? parent = Path.GetDirectoryName(path: dir);
            if (parent == null || parent == dir)
            {
                break;
            }

            dir = parent;
        }

        throw new InvalidOperationException(
            message: "Could not locate Mielia.csproj walking up from test assembly directory.");
    }
}

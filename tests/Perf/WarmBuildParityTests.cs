using System.Text.RegularExpressions;
using Builder.Verification;
using TypeModel.Enums;

namespace Mielia.Tests.Perf;

/// <summary>
/// A Mielia program built again and again from one stdlib snapshot (the daemon's edit loop) defines the same
/// routines every time.
/// </summary>
public sealed partial class WarmBuildParityTests
{
    /// <summary>Mielia's <c>get_count() -> Int</c> is a library default copied into each emitter type, and its
    /// body calls the shared <c>get_count() -> U64</c> of the same name. From the second warm build on, the copy
    /// bound that inner call to itself, so the shared routine was never defined and the IR did not parse.</summary>
    [Fact]
    public void RepeatedWarmBuild_OfASurfaceDefault_DefinesTheSharedRoutineEachTime()
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"anv_warm_parity_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: dir);
        try
        {
            string file = Path.Combine(path1: dir, path2: "main.mie");
            File.WriteAllText(path: file, contents: """
                                                    import EmitterTools

                                                    record Point
                                                        x: Int

                                                    routine start()
                                                        var points: List[Point] = [Point(1), Point(5)]
                                                        show(points.where(p => p.x > 2).get_count())
                                                        return
                                                    """);
            SemanticVerifier.CompiledStdlibState warm =
                SemanticVerifier.CaptureCompiledStdlib(language: Language.Mielia);
            // The first warm build may also define a few routines nothing calls, so the builds after it are the ones
            // that must agree.
            HashSet<string>? second = null;
            for (int build = 1; build <= 3; build++)
            {
                Assert.Equal(expected: 0,
                    actual: Builder.Execution.Program.CompileEntryToIrForTests(entryFile: file, ir: out string ir,
                        warm: warm));
                HashSet<string> defined = DefinedSymbols(ll: ir);
                Assert.True(condition: defined.Contains(item: SharedGetCount),
                    userMessage: $"warm build {build} does not define {SharedGetCount}");
                if (build == 1)
                {
                    SemanticVerifier.PrepareNextRestore(warm: warm);
                    continue;
                }

                second ??= defined;
                string[] missing = second.Except(second: defined).Order().ToArray();
                string[] added = defined.Except(second: second).Order().ToArray();
                Assert.True(condition: missing.Length == 0 && added.Length == 0,
                    userMessage: $"warm build {build}: missing=[{string.Join(separator: ", ", values: missing.Take(count: 6))}] " +
                                 $"added=[{string.Join(separator: ", ", values: added.Take(count: 6))}]");
                // What the daemon does between two builds.
                SemanticVerifier.PrepareNextRestore(warm: warm);
            }
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    private const string SharedGetCount =
        "[member] EmitterTools.WhereEmittable[Main.Point, ME::System.List[Main.Point]].get_count()";

    [GeneratedRegex(pattern: "^define [^@]*@\"([^\"]+)\"", options: RegexOptions.Multiline)]
    private static partial Regex DefineRegex();

    private static HashSet<string> DefinedSymbols(string ll)
    {
        return DefineRegex().Matches(input: ll)
                            .Select(selector: m => m.Groups[groupnum: 1].Value)
                            .ToHashSet(comparer: StringComparer.Ordinal);
    }
}

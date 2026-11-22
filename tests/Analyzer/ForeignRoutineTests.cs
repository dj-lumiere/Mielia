using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Mielia.Tests.Analyzer;

/// <summary>
/// A foreign routine in a Mielia file is ME-S810 once, at its declaration, and the messages that send code to a
/// `.rvr` module (ME-S810, ME-S800) end with the line that points to the wiki section showing such a module.
/// </summary>
public class ForeignRoutineTests
{
    /// <summary>The wiki section the messages point to.</summary>
    private const string WikiSection = "https://mielia.lumi-dev.xyz/docs/appendix-revare/#reaching-c";

    /// <summary>The errors of analyzing <paramref name="source"/> as the Mielia file <c>main.mie</c>.</summary>
    private static List<SemanticError> BuildErrors(string source)
    {
        return TestHelpers.AnalyzeSaMielia(source: source, fileName: "main.mie").Errors.ToList();
    }

    /// <summary>A <c>C::</c> routine declared and called in the same file is one ME-S810, at the declaration, and its
    /// message ends with the line pointing to the wiki section.</summary>
    [Fact]
    public void Build_ForeignRoutineDeclaredAndCalled_ReportsOnlyTheDeclaration()
    {
        List<SemanticError> errors = BuildErrors(source: """
                                                         import IO/Console

                                                         routine C::labs(n: S64) -> S64

                                                         routine start()
                                                             show(C::labs(n: -4))
                                                             show(C::labs(n: -5))
                                                         """);
        SemanticError error = Assert.Single(collection: errors,
            predicate: e => e.Code == SemanticDiagnosticCode.MieliaNoCInterop);
        Assert.Contains(expectedSubstring: "You declare the foreign routine 'labs'", actualString: error.Message);
        Assert.Equal(expected: 3, actual: error.Location.Line);
        Assert.EndsWith(expectedEndString: "\nA `.rvr` module that wraps a C library: " + WikiSection,
            actualString: error.Message);
    }

    /// <summary>A <c>danger</c> block in a Mielia file is ME-S800, and its message ends with the same line.</summary>
    [Fact]
    public void Build_DangerBlock_PointsToTheWikiSection()
    {
        List<SemanticError> errors = BuildErrors(source: """
                                                         import IO/Console

                                                         routine start()
                                                             danger
                                                                 show("hi")
                                                         """);
        SemanticError error = Assert.Single(collection: errors,
            predicate: e => e.Code == SemanticDiagnosticCode.FeatureNotInMielia);
        Assert.EndsWith(expectedEndString: WikiSection, actualString: error.Message);
    }
}

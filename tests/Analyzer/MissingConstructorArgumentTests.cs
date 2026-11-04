using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Mielia.Tests.Analyzer;

using static TestHelpers;

/// <summary>A construction with no arguments of a type whose member variables have no defaults names them (MI-S455).</summary>
public class MissingConstructorArgumentTests
{
    /// <summary><c>throw IndexOutOfBoundsError()</c> names <c>index</c> and <c>count</c> and the whole call.</summary>
    [Fact]
    public void Analyze_CrashableWithNoArguments_NamesTheMissingMemberVariables()
    {
        string source = """
                        routine pick(k: S64) -> S64
                            if k < 0
                                throw IndexOutOfBoundsError()
                            return k

                        routine start()
                            show(pick(k: 3))
                        """;

        AnalysisResult result = AnalyzeSaMielia(source: source);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.MissingRequiredMemberVariable &&
                         e.Message.Contains(value: "'IndexOutOfBoundsError(index: ..., count: ...)'"));
    }
}

using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// Arguments without a label fill the parameters in order, and a bare name of another parameter in the wrong place
/// needs a label.
/// </summary>
public class PositionalArgumentTests
{
    /// <summary>Three arguments without labels fill the three parameters in order.</summary>
    [Fact]
    public void Analyze_ThreeUnlabeledArguments_FillInOrder()
    {
        AnalysisResult result = AnalyzeSaSuflae(source: """
                                       routine volume(width: Int, height: Int, depth: Int) -> Int
                                           return width * height * depth

                                       routine start()
                                           var total = volume(2, 3, 4)
                                           return
                                       """);

        Assert.Empty(collection: result.Errors);
    }

    /// <summary>A bare name of another parameter in a different parameter's place needs a label.</summary>
    [Fact]
    public void Analyze_NameOfAnotherParameterInTheWrongPlace_NeedsLabel()
    {
        AnalysisResult result = AnalyzeSaSuflae(source: """
                                       routine move(count: Int, from: Int, tmp: Int, to: Int) -> Int
                                           return count

                                       routine start()
                                           var from = 1
                                           var tmp = 2
                                           var to = 3
                                           var a = move(1, from, to, tmp)
                                           var b = move(1, from, tmp: to, to: tmp)
                                           return
                                       """);

        Assert.Equal(expected: 2,
            actual: result.Errors.Count(predicate: e => e.Code == SemanticDiagnosticCode.NamedArgumentRequired));
    }
}

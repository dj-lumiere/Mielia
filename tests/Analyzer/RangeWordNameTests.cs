using Builder.Verification.Results;

namespace Suflae.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// <c>to</c>, <c>til</c> and <c>by</c> are keywords only inside a range expression. Everywhere else they are names:
/// parameters, labels, member variables, bindings taken from a tuple.
/// </summary>
public class RangeWordNameTests
{
    [Fact]
    public void Analyze_RangeWordsAsNames_Build()
    {
        AnalysisResult result = AnalyzeSaSuflae(source: """
                                                record Step
                                                    from: Int
                                                    to: Int
                                                    by: Int
                                                    til: Int

                                                routine hop(n: Int, to: Int, by: Int, til: Int) -> Int
                                                    return n + by + til

                                                routine start()
                                                    var (n, from, tmp, to) = (1, 2, 3, 4)
                                                    var step = Step(from: 1, to: 2, by: 3, til: 4)
                                                    var moved = hop(n: 1, to: 5, by: 2, til: 3)
                                                    var by = 2
                                                    var til = 9
                                                    var sum = to + by
                                                    for i in 0 to til by by
                                                        var seen = i
                                                    return
                                                """);

        Assert.Empty(collection: result.Errors);
    }
}

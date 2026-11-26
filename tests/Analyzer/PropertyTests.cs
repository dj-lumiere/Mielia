using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Mielia.Tests.Analyzer;

using static TestHelpers;

/// <summary>A Mielia <c>@property</c> is checked on its own body only: a write to <c>me</c> there is ME-S718, and a
/// call of another routine on <c>me</c> is not looked into.</summary>
public class PropertyTests
{
    private const string Cow = """
                               entity Cow
                                   weight: Int

                               routine Cow.feed()
                                   me.weight += 1

                               """;

    [Fact]
    public void Analyze_PropertyCallingChangingRoutine_Analyzes()
    {
        AnalysisResult result = AnalyzeSaMielia(source: Cow + """
                                                              @property
                                                              routine Cow.fed() -> Int
                                                                  me.feed()
                                                                  return me.weight

                                                              routine start()
                                                                  var c = Cow(weight: 3)
                                                                  var n: Int = c.fed
                                                              """,
            fileName: "property_calling_changing_routine.mie");
        Assert.True(condition: result.Errors.Count == 0,
            userMessage: string.Join(separator: "\n", values: result.Errors.Select(selector: e => e.Message)));
    }

    [Fact]
    public void Analyze_PropertyWritingMe_Reports()
    {
        AnalysisResult result = AnalyzeSaMielia(source: Cow + """
                                                              @property
                                                              routine Cow.grow() -> Int
                                                                  me.weight += 1
                                                                  return me.weight

                                                              routine start()
                                                                  var c = Cow(weight: 3)
                                                                  var n: Int = c.grow
                                                              """,
            fileName: "property_writing_me.mie");
        Assert.NotEmpty(collection: result.Errors);
        Assert.All(collection: result.Errors,
            action: e => Assert.Equal(expected: SemanticDiagnosticCode.PropertyChangesMe, actual: e.Code));
    }
}

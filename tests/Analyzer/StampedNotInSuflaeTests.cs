using Builder.Diagnostics;
using Builder.Verification.Results;
using static Suflae.Tests.TestHelpers;

namespace Suflae.Tests.Analyzer;

/// <summary>
/// A Suflae entity is a shared handle and Suflae's library binds no <c>Stamped</c>, so calling an entity's
/// <c>stamp()</c> in a Suflae program is a build error at the call (SF-S447), never a crash when it runs, and the
/// message about a type without a stamp slot (which points to <c>Stamped</c>) is not shown.
/// </summary>
public class StampedNotInSuflaeTests
{
    [Fact]
    public void Analyze_SuflaeCallsStamp_ReportsS447Only()
    {
        AnalysisResult result = AnalyzeSuflae(fileName: "stamped_not_in_suflae.sf", source: """
                                                      entity Node
                                                          value: S64

                                                      routine start()
                                                          var n = Node(value: 1)
                                                          var s = n.stamp()
                                                          return
                                                      """);
        SemanticError error = Assert.Single(collection: result.Errors,
            predicate: e => e.Code == SemanticDiagnosticCode.UnsharedRazorForgeDeclaration);
        Assert.Contains(expectedSubstring: "stamp", actualString: error.Message);
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.StampOfTypeWithoutSlot);
    }
}

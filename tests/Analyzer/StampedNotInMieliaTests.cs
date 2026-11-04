using Builder.Diagnostics;
using Builder.Verification.Results;
using static Mielia.Tests.TestHelpers;

namespace Mielia.Tests.Analyzer;

/// <summary>
/// A Mielia entity is a shared handle and Mielia's library binds no <c>Stamped</c>, so calling an entity's
/// <c>stamp()</c> in a Mielia program is a build error at the call (MI-S447), never a crash when it runs, and the
/// message about a type without a stamp slot (which points to <c>Stamped</c>) is not shown.
/// </summary>
public class StampedNotInMieliaTests
{
    [Fact]
    public void Analyze_MieliaCallsStamp_ReportsS447Only()
    {
        AnalysisResult result = AnalyzeMielia(fileName: "stamped_not_in_mielia.mi", source: """
                                                      entity Node
                                                          value: S64

                                                      routine start()
                                                          var n = Node(value: 1)
                                                          var s = n.stamp()
                                                          return
                                                      """);
        SemanticError error = Assert.Single(collection: result.Errors,
            predicate: e => e.Code == SemanticDiagnosticCode.UnsharedRevareDeclaration);
        Assert.Contains(expectedSubstring: "stamp", actualString: error.Message);
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.StampOfTypeWithoutSlot);
    }
}

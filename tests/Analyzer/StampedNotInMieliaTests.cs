using Builder.Diagnostics;
using Builder.Verification.Results;
using static Mielia.Tests.TestHelpers;

namespace Mielia.Tests.Analyzer;

/// <summary>
/// A Mielia entity is a shared handle and Mielia's library binds no <c>PoolManager</c> or <c>Stamped</c>, so setting a
/// pool up in a Mielia program is a build error at the call (ME-S447), never a crash when it runs.
/// </summary>
public class StampedNotInMieliaTests
{
    [Fact]
    public void Analyze_MieliaCallsStamp_ReportsS447Only()
    {
        AnalysisResult result = AnalyzeMielia(fileName: "stamped_not_in_mielia.mie", source: """
                                                      entity Node
                                                          value: S64

                                                      routine start()
                                                          var pool = PoolManager.set_up()
                                                          return
                                                      """);
        SemanticError error = Assert.Single(collection: result.Errors,
            predicate: e => e.Code == SemanticDiagnosticCode.UnsharedRevareDeclaration);
        Assert.Contains(expectedSubstring: "PoolManager", actualString: error.Message);
    }
}

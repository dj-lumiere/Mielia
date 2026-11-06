using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Mielia.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// A <c>secret</c> name declared in the standard library is seen by every library file and by no program.
/// </summary>
public class LibrarySecretTests
{
    /// <summary>A program naming the library's <c>Places</c> row gets ME-S402.</summary>
    [Fact]
    public void Analyze_ProgramNamesLibrarySecretType_Fails()
    {
        AnalysisResult result = AnalyzeSaMielia(source: """
                                       routine start()
                                           var p = Places[S64](capacity: 4)
                                           return
                                       """);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.SecretTypeAccess);
    }
}

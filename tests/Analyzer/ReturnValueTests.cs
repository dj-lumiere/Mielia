using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Mielia.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// The declaration states what a routine gives back. A routine without <c>-&gt; T</c> gives nothing back, so a
/// <c>return</c> with a value in it is ME-S301, and a bare <c>return</c> in a routine that gives back a value is
/// ME-S301 too. The value never sets the result type.
/// </summary>
public class ReturnValueTests
{
    [Fact]
    public void Analyze_ValueReturnWithoutArrow_ReportsError()
    {
        string source = """
                        record Point
                            x: Int

                        routine is_big(p: Point)
                            return p.x > 3

                        show(is_big(p: Point(x: 5)))
                        """;

        AnalysisResult result = AnalyzeSaMielia(source: source, fileName: "value_without_arrow.mie");
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.ReturnTypeMismatch &&
                         e.Message.Contains(value: "'is_big' gives nothing back") &&
                         e.Message.Contains(value: "'-> Bool'"));
    }

    [Fact]
    public void Analyze_BareReturnInValueRoutine_ReportsError()
    {
        string source = """
                        routine is_big(x: Int) -> Bool
                            if x > 3
                                return
                            return false
                        """;

        AnalysisResult result = AnalyzeSaMielia(source: source, fileName: "bare_in_value.mie");
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.ReturnTypeMismatch &&
                         e.Message.Contains(value: "'is_big' gives back a 'Bool'"));
    }

    [Fact]
    public void Analyze_MixedBareAndValueReturns_ReportsError()
    {
        string source = """
                        routine mixed(x: Int)
                            if x > 3
                                return x > 4
                            return
                        """;

        AnalysisResult result = AnalyzeSaMielia(source: source, fileName: "mixed_returns.mie");
        Assert.Single(collection: result.Errors,
            predicate: e => e.Code == SemanticDiagnosticCode.ReturnTypeMismatch &&
                            e.Message.Contains(value: "'mixed' gives nothing back"));
    }

    /// <summary>A Mielia routine that gives nothing back ends without <c>return</c>, and an early bare
    /// <c>return</c> in it is fine.</summary>
    [Fact]
    public void Analyze_NoneRoutineEndsWithoutReturn_NoError()
    {
        string source = """
                        routine greet(x: Int)
                            if x > 3
                                return
                            show("hello")

                        greet(x: 5)
                        """;

        AnalysisResult result = AnalyzeSaMielia(source: source, fileName: "none_without_return.mie");
        Assert.DoesNotContain(collection: result.Errors,
            filter: e => e.Code is SemanticDiagnosticCode.ReturnTypeMismatch or SemanticDiagnosticCode.MissingReturn);
    }

    /// <summary>A lambda's value that does not fit the <c>Callable</c> it goes to is ME-S502.</summary>
    [Fact]
    public void Analyze_LambdaValueIntoNoneCallable_ReportsError()
    {
        string source = """
                        routine run(f: Callable[(Int,), None])
                            f(3)

                        run(f: x => x > 3)
                        """;

        AnalysisResult result = AnalyzeSaMielia(source: source, fileName: "lambda_into_none.mie");
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.ArgumentTypeMismatch);
    }
}

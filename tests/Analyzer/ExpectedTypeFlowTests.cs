using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Mielia.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// A call whose type argument comes only from its result (<c>n.to()</c>) takes it from where the value goes. The
/// expected type flows inward: from a declared variable type, an assignment, a compound assignment, a parameter and a
/// return type, into both operands of an operator whose routine takes and gives <c>Me</c>, across a comparison, and
/// down a chain of member calls that give <c>Me</c> back. It never comes from later uses of a variable.
/// </summary>
public class ExpectedTypeFlowTests
{
    private static string Program(string body) => $$"""
                                                   import IO/Console

                                                   routine half(x: B64) -> B64
                                                       return x / 2.0

                                                   routine widened(n: S64) -> B64
                                                       return n.to()

                                                   routine B64.positive() -> B64?
                                                       if me > 0.0
                                                           return me
                                                       return none

                                                   var n: S64 = 9
                                                   var a: B64 = 0.0
                                                   {{body}}
                                                   show(a)
                                                   """;

    private static void AssertBuilds(string body)
    {
        AnalysisResult result = AnalyzeSaMielia(source: Program(body: body), fileName: "expected_type_flow.mie");
        Assert.True(condition: result.Errors.Count == 0,
            userMessage: string.Join(separator: "\n", values: result.Errors.Select(selector: e => e.Message)));
    }

    /// <summary>The line this rule was written for.</summary>
    [Fact]
    public void Analyze_CompoundAssignmentThroughOperatorAndChain_Builds()
    {
        AssertBuilds(body: """
                           var result: B128 = 0.0
                           for i in S64(1) to 10
                               result += i.to() * i.to().sqrt().sin()
                           show(result)
                           """);
    }

    [Fact]
    public void Analyze_Assignment_Builds() => AssertBuilds(body: "a = n.to()");

    [Fact]
    public void Analyze_OperatorOperands_Build() => AssertBuilds(body: "a = n.to() * n.to() + a");

    [Fact]
    public void Analyze_Comparison_Builds() => AssertBuilds(body: "if n.to() < a\n    show(\"less\")");

    [Fact]
    public void Analyze_ChainOfMeRoutines_Builds() => AssertBuilds(body: "a = n.to().sqrt().abs()");

    [Fact]
    public void Analyze_ArgumentAndReturn_Build() => AssertBuilds(body: "a = half(x: n.to()) + widened(n: n)");

    /// <summary>A variable's type never comes from where it is used later, and the message names the places looked
    /// at.</summary>
    [Fact]
    public void Analyze_UntypedVariable_StaysAmbiguous()
    {
        AnalysisResult result = AnalyzeSaMielia(source: Program(body: "var z = n.to()\na = z"),
            fileName: "expected_type_untyped.mie");
        SemanticError error = Assert.Single(collection: result.Errors,
            predicate: e => e.Code == SemanticDiagnosticCode.ResultTypeArgumentUnknown);
        Assert.Contains(expectedSubstring: "The builder looks at a written variable type", actualString: error.Message);
    }

    [Fact]
    public void Analyze_LeftOperandFromRight_Builds() => AssertBuilds(body: "var x: B128 = 2.0\nshow(n.to() * x)");

    [Fact]
    public void Analyze_ChainedComparison_Builds() =>
        AssertBuilds(body: "var b: B64 = 10.0\nif 0.0 <= n.to() <= b\n    show(\"inside\")");

    [Fact]
    public void Analyze_MaybeChain_Builds() => AssertBuilds(body: "var p: B64? = n.to().positive()\nshow(p)");

    /// <summary>An operator routine that fits more than one way gives no type, and the message names its
    /// routines.</summary>
    [Fact]
    public void Analyze_SeveralOperatorRoutines_NamesThem()
    {
        AnalysisResult result = AnalyzeSaMielia(source: Program(body: "show(a + n.to())"), fileName: "expected_type_several.mie");
        SemanticError error = Assert.Single(collection: result.Errors,
            predicate: e => e.Code == SemanticDiagnosticCode.ResultTypeArgumentUnknown);
        Assert.Contains(expectedSubstring: "'B64.add' fits more than one way here", actualString: error.Message);
    }

    /// <summary>An argument that failed while the overload was looked for is reported once, not again against
    /// another overload's parameter.</summary>
    [Fact]
    public void Analyze_FailedArgument_ReportedOnce()
    {
        AnalysisResult result = AnalyzeSaMielia(source: Program(body: "show(n.to())"), fileName: "expected_type_once.mie");
        SemanticError error = Assert.Single(collection: result.Errors);
        Assert.Equal(expected: SemanticDiagnosticCode.ResultTypeArgumentUnknown, actual: error.Code);
    }
}

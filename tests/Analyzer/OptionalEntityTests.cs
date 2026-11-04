using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Mielia.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// An optional entity <c>E?</c> is a type of its own: <c>none</c> goes only where the type says the entity may be
/// missing, a list's type argument included, and a checked name is the entity itself.
/// </summary>
public class OptionalEntityTests
{
    /// <summary><c>none</c> into a list of entities that always hold one (<c>List[Box]</c>) doesn't build.</summary>
    [Fact]
    public void Analyze_NoneIntoListOfEntities_Fails()
    {
        AnalysisResult result = AnalyzeSaMielia(source: """
                                       entity Box
                                           n: Int

                                       routine start()
                                           var xs = List[Box]()
                                           xs.add_last(none)
                                       """);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.AssignmentTypeMismatch);
    }

    /// <summary><c>none</c> into a list of optional entities (<c>List[Box?]</c>) builds.</summary>
    [Fact]
    public void Analyze_NoneIntoListOfOptionalEntities_Builds()
    {
        AnalysisResult result = AnalyzeSaMielia(source: """
                                       entity Box
                                           n: Int

                                       routine start()
                                           var xs = List[Box?]()
                                           xs.add_last(value: none)
                                           xs.add_last(value: Box(n: 1))
                                           return
                                       """);
        Assert.Empty(collection: result.Errors);
    }

    /// <summary>An <c>E?</c> handed to a parameter that takes an <c>E</c> doesn't build until it is checked.</summary>
    [Fact]
    public void Analyze_OptionalEntityIntoEntityParameter_Fails()
    {
        AnalysisResult result = AnalyzeSaMielia(source: """
                                       entity Box
                                           n: Int

                                       routine take(b: Box) -> Int
                                           return b.n

                                       routine start()
                                           var m: Box? = none
                                           show(take(b: m))
                                       """);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.ArgumentTypeMismatch);
    }

    /// <summary>After a <c>when</c> arm for <c>None</c> that returns, the name is the entity itself.</summary>
    [Fact]
    public void Analyze_WhenNoneArmReturns_NameIsNarrowed()
    {
        AnalysisResult result = AnalyzeSaMielia(source: """
                                       entity Box
                                           n: Int

                                       routine size(b: Box?) -> Int
                                           when b
                                               is None => return 0
                                           return b.n

                                       routine start()
                                           var n = size(b: Box(n: 3))
                                           return
                                       """);
        Assert.Empty(collection: result.Errors);
    }

    /// <summary>Reading through an <c>E?</c> before checking it is MI-S619.</summary>
    [Fact]
    public void Analyze_ReadThroughUncheckedOptionalEntity_Fails()
    {
        AnalysisResult result = AnalyzeSaMielia(source: """
                                       entity Box
                                           n: Int

                                       routine start()
                                           var m: Box? = Box(n: 1)
                                           show(m.n)
                                       """);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.NullableEntityDeref);
    }

    /// <summary>A field path a check narrowed reads as the entity, until the path is assigned.</summary>
    [Fact]
    public void Analyze_MemberPathChecked_IsTheEntity()
    {
        AnalysisResult result = AnalyzeSaMielia(source: """
                                       entity Cell
                                           next: Cell?
                                           value: Int

                                       routine sum(c: Cell) -> Int
                                           if c.next is None
                                               return c.value
                                           return c.value + c.next.value

                                       routine second(c: Cell) -> Int
                                           if c.next isnot None
                                               if c.next.next isnot None
                                                   return c.next.next.value
                                           return 0
                                       """);
        Assert.Empty(collection: result.Errors);
    }

    /// <summary>Assigning the path puts the optional back.</summary>
    [Fact]
    public void Analyze_MemberPathAssigned_IsOptionalAgain()
    {
        AnalysisResult result = AnalyzeSaMielia(source: """
                                       entity Cell
                                           next: Cell?
                                           value: Int

                                       routine again(c: Cell) -> Int
                                           if c.next is None
                                               return 0
                                           c.next = none
                                           return c.next.value
                                       """);
        Assert.Contains(collection: result.Errors,
            filter: e => e.Code == SemanticDiagnosticCode.NullableEntityDeref);
    }
}

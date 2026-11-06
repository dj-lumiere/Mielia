using Builder.Diagnostics;
using Builder.Verification.Results;

namespace Mielia.Tests.Analyzer;

using static TestHelpers;

/// <summary>
/// A Mielia result left unused is a notice: the line builds as written, and check, lint and the language server ask
/// whether the result was meant.
/// </summary>
public class NoticeTests
{
    [Fact]
    public void Analyze_MieliaUnusedResult_IsANoticeNotAWarning()
    {
        AnalysisResult result = AnalyzeMielia(source: """
                                                        routine get_value() -> Text
                                                            return "x"

                                                        routine test()
                                                            get_value()
                                                            return
                                                        """, fileName: "unused_result.mie");
        Assert.Empty(collection: result.Errors);
        Assert.DoesNotContain(collection: result.Warnings,
            filter: w => w.Code == SemanticWarningCode.UnusedRoutineReturnValue);
        Assert.Contains(collection: result.Notices,
            filter: n => n.Code == SemanticWarningCode.UnusedRoutineReturnValue && n.IsNotice);
    }

    private static List<SemanticWarning> DocNotes(IEnumerable<SemanticWarning> notices)
    {
        return notices.Where(predicate: n => n.Code is >= SemanticWarningCode.RoutineHasNoDocComment
                                                 and <= SemanticWarningCode.DocCommentLacksReturns)
                      .ToList();
    }

    [Fact]
    public void DocNotes_WrongParamLines_AreNotesEverywhere_NoDocOnlyWhenImported()
    {
        List<SemanticWarning> alone = DocNotes(notices: AnalyzeMielia(source: """
                                                                              routine bare(x: Int) -> Int
                                                                                  return x

                                                                              ### Adds.
                                                                              ### :param a: the first
                                                                              ### :param c: not a parameter
                                                                              routine add(a: Int, b: Int) -> Int
                                                                                  return a + b
                                                                              """,
            fileName: "/virtual/docs/doc_notes.mie").Notices);
        Assert.DoesNotContain(collection: alone, filter: n => n.Code == SemanticWarningCode.RoutineHasNoDocComment);
        Assert.DoesNotContain(collection: alone, filter: n => n.Code == SemanticWarningCode.DocCommentLacksReturns);
        Assert.Contains(collection: alone, filter: n => n.Code == SemanticWarningCode.DocCommentLacksParam &&
                                                       n.Message.Contains(value: "'b'") && n.CodeString == "ME-W306");
        Assert.Contains(collection: alone, filter: n => n.Code == SemanticWarningCode.DocCommentNamesUnknownParam &&
                                                       n.Message.Contains(value: "'c'"));
    }

    [Fact]
    public void DocNotes_RoutineOfAnImportedModule_NeedsADocComment()
    {
        const string lib = "/virtual/docs/doclib.mie";
        const string entry = "/virtual/docs/docuse.mie";
        var libProgram = ParseMielia(source: "module DocLib\n\nroutine bare(x: Int) -> Int\n    return x\n", fileName: lib);
        var entryProgram = ParseMielia(source: "import DocLib\n\nroutine start()\n    return\n", fileName: entry);
        var analyzer = new Builder.Verification.SemanticVerifier(language: TypeModel.Enums.Language.Mielia);
        AnalysisResult result = analyzer.AnalyzeMultiple(files: [(libProgram, lib), (entryProgram, entry)]);
        List<SemanticWarning> notes = DocNotes(notices: result.Notices);
        Assert.Contains(collection: notes, filter: n => n.Code == SemanticWarningCode.RoutineHasNoDocComment &&
                                                       n.Location.FileName == lib && n.CodeString == "ME-W305");
        Assert.DoesNotContain(collection: notes, filter: n => n.Location.FileName == entry);
    }
}

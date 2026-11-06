using Builder.Declaration;
using Builder.Diagnostics;
using Builder.Verification.Results;
using TypeModel.Enums;

namespace Mielia.Tests.Analyzer;

/// <summary>
/// A Mielia file may import only the modules the Mielia standard library declares: a module that only the Revare
/// standard library has (BuilderQuery, Simd) is ME-S116 at the import, through the real build driver.
/// </summary>
public class RevareOnlyImportTests
{
    /// <summary>The errors of building <paramref name="source"/> as a Mielia entry file.</summary>
    private static List<SemanticError> BuildErrors(string source)
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: "sf-rf-only-" + Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: dir);
        try
        {
            string entry = Path.Combine(path1: dir, path2: "main.mie");
            File.WriteAllText(path: entry, contents: source);
            var driver = new BuildDriver(projectRoot: dir, stdlibRoot: StdlibLoader.GetDefaultStdlibPath(),
                language: Language.Mielia);
            return driver.CompileFile(entryFile: entry).Errors;
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    /// <summary><c>import BuilderQuery</c> and <c>import Simd</c> in a Mielia file are each an ME-S116 that names the module.</summary>
    [Theory]
    [InlineData("BuilderQuery")]
    [InlineData("Simd")]
    public void Build_MieliaImportsRevareOnlyModule_ReportsS116(string module)
    {
        List<SemanticError> errors = BuildErrors(source: $"""
                                                          import {module}
                                                          import IO/Console

                                                          routine start()
                                                              show("hi")
                                                          """);
        SemanticError error = Assert.Single(collection: errors,
            predicate: e => e.Code == SemanticDiagnosticCode.ModuleNotInLanguageStandardLibrary);
        Assert.Contains(expectedSubstring: $"'{module}' isn't a module of the Mielia standard library", actualString: error.Message);
    }

    /// <summary>A module the Mielia standard library declares imports as before.</summary>
    [Fact]
    public void Build_MieliaImportsItsOwnModule_HasNoS116()
    {
        List<SemanticError> errors = BuildErrors(source: """
                                                         import Collections
                                                         import IO/Console

                                                         routine start()
                                                             show("hi")
                                                         """);
        Assert.DoesNotContain(collection: errors,
            filter: e => e.Code == SemanticDiagnosticCode.ModuleNotInLanguageStandardLibrary);
    }
}

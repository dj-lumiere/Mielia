using SyntaxTree;

namespace Mielia.Tests.Parser;

using static TestHelpers;

/// <summary>
/// A script whose first statement takes a value apart (<c>var (a, b) = (1, 2)</c>) parses like any other first
/// statement: the location comes from the <c>var</c> itself, which has no token before it at the start of the file.
/// </summary>
public sealed class ScriptDestructuringTests
{
    [Fact]
    public void Parse_DestructuringAsFirstScriptStatement_Parses()
    {
        AssertParsesMielia(source: "var (a, b) = (1, 2)\nshow(a + b)\n");
    }
}

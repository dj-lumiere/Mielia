using SyntaxTree;

namespace Mielia.Tests.Parser;

using static TestHelpers;

/// <summary>
/// A member routine named with a keyword (<c>for</c>) is a grammar error that says the word is a keyword, in the
/// declaration and at the call, the same as in Revare.
/// </summary>
public sealed class KeywordNameTests
{
    [Fact]
    public void Parse_KeywordAsMemberRoutineName_SaysItIsAKeyword()
    {
        string source = """
                        entity Pile
                            n: S64

                        routine Pile.for(by: S64)
                            me.n += by
                            return

                        routine start()
                            var p = Pile(n: 1)
                            p.for(by: 2)
                            return
                        """;

        (Program _, Builder.Parser.Parser parser) = ParseMieliaWithErrors(source: source);
        List<string> messages = parser.GetStructuredErrors().Select(selector: e => e.Message).ToList();
        Assert.Equal(expected: 2, actual: messages.Count);
        Assert.All(collection: messages,
            action: m => Assert.Contains(expectedSubstring: "'for' is a keyword, so it cannot name anything",
                actualString: m));
    }
}

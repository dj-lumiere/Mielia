using System.Text.Json;

namespace Mielia.Tests.Execution;

/// <summary>
/// End-to-end coverage for the LSP server (<c>src/Execution/LspServer.cs</c>): every request handler
/// is driven in-process over real, analyzed Revare/Mielia documents via <see cref="LspTestHarness"/>.
/// Positions are computed from the source text so the tests stay robust to edits.
///
/// All tests live in one class so they share xUnit's per-class sequential execution — the server keeps
/// open documents in a process-wide static dictionary, and each <see cref="LspTestHarness.Run"/> clears
/// it at the start, so overlapping sessions from parallel classes would race.
/// </summary>
public sealed class LspServerTests
{
    private const string Uri = "file:///test/Sample.rvr";

    // A symbol-rich Revare document: free routines, a record with a member routine, a generic
    // container use, variables, calls, an f-string, and control flow — enough surface to exercise
    // hover / definition / references / completion / signature-help / symbols / semantic tokens.
    private const string Source =
        "module Test/Lsp\n" +
        "import IO/Console\n" +
        "\n" +
        "record Point\n" +
        "    x: S32\n" +
        "    y: S32\n" +
        "\n" +
        "    routine magnitude(me) -> S32\n" +
        "        return me.x * me.x + me.y * me.y\n" +
        "\n" +
        "routine add(a: S32, b: S32) -> S32\n" +
        "    return a + b\n" +
        "\n" +
        "routine start()\n" +
        "    var total = add(a: 1, b: 2)\n" +
        "    var p = Point(x: 3, y: 4)\n" +
        "    var m = p.magnitude()\n" +
        "    show(f\"total={total} mag={m}\")\n" +
        "    return\n";

    private static string Initialize(int id = 0)
    {
        return JsonSerializer.Serialize(value: new
        {
            jsonrpc = "2.0",
            id,
            method = "initialize",
            @params = new { capabilities = new { } }
        });
    }

    private const string Shutdown = "{\"jsonrpc\":\"2.0\",\"id\":999,\"method\":\"shutdown\"}";
    private const string Exit = "{\"jsonrpc\":\"2.0\",\"method\":\"exit\"}";

    private static (int Line, int Char) Pos(string needle, int occurrence = 1)
    {
        return LspTestHarness.PositionOf(text: Source, needle: needle, occurrence: occurrence);
    }

    /// <summary>The labels of a <c>textDocument/completion</c> reply.</summary>
    private static List<string> CompletionLabels(JsonElement? reply)
    {
        return reply!.Value.GetProperty(propertyName: "result")
                     .GetProperty(propertyName: "items")
                     .EnumerateArray()
                     .Select(selector: item => item.GetProperty(propertyName: "label").GetString() ?? "")
                     .ToList();
    }

    /// <summary>The <c>serverInfo.name</c> of an <c>initialize</c> reply.</summary>
    private static string? ServerName(JsonElement? reply)
    {
        return reply!.Value.GetProperty(propertyName: "result")
                     .GetProperty(propertyName: "serverInfo")
                     .GetProperty(propertyName: "name")
                     .GetString();
    }

    [Fact]
    public void Initialize_NamesTheMieliaServer()
    {
        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(Initialize(id: 1), Shutdown, Exit);

        Assert.Equal(expected: "mielia-lsp", actual: ServerName(reply: LspTestHarness.ReplyWithId(replies: replies, id: 1)));
    }

    [Fact]
    public void RevareDocument_IsLeftToTheRevareServer()
    {
        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(
            LspTestHarness.DidOpen(uri: Uri, text: Source),
            Exit);

        // A .rvr file is the Revare server's: this one publishes nothing for it.
        Assert.Null(@object: LspTestHarness.Diagnostics(replies: replies, uri: Uri));
    }

    [Fact]
    public void Completion_OffersOnlyMieliaKeywords()
    {
        const string miUri = "file:///test/Keywords.mie";
        const string sf =
            "routine start()\n" +
            "    var total = 1\n" +
            "    return\n";
        (int line, int character) = LspTestHarness.PositionOf(text: sf, needle: "total");
        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(
            LspTestHarness.DidOpen(uri: miUri, text: sf, languageId: "mielia"),
            LspTestHarness.Positional(id: 40, method: "textDocument/completion", uri: miUri, line: line,
                character: character + 1),
            Exit);

        List<string> labels = CompletionLabels(reply: LspTestHarness.ReplyWithId(replies: replies, id: 40));
        Assert.Contains(expected: "routine", collection: labels);
        // Revare-only keywords are not Mielia's.
        Assert.DoesNotContain(expected: "steal", collection: labels);
    }

    [Theory]
    [InlineData("routine start()\n    var m = Moment.epoch()\n    return\n", "Moment", "absolute point in time")]
    [InlineData("routine start()\n    var d = Duration.zero()\n    return\n", "Duration", "span of time")]
    [InlineData("### Two numbers side by side.\nrecord Pair\n    a: S64\n    b: S64\n\nroutine start()\n    var p = Pair(a: 1, b: 2)\n    return\n", "Pair(", "side by side")]
    public void Hover_OverType_ShowsItsDocComment(string source, string needle, string doc)
    {
        // A Mielia standard library type is shown with `@rv(...)` between its doc comment and its header.
        string dir = Directory.CreateTempSubdirectory(prefix: "lsp_hover_").FullName;
        string path = Path.Combine(path1: dir, path2: "main.mie");
        File.WriteAllText(path: path, contents: source);
        string uri = new System.Uri(uriString: path).AbsoluteUri;
        (int line, int character) = LspTestHarness.PositionOf(text: source, needle: needle);
        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(
            LspTestHarness.DidOpen(uri: uri, text: source, languageId: "mielia"),
            LspTestHarness.Positional(id: 9, method: "textDocument/hover", uri: uri, line: line, character: character),
            Exit);

        JsonElement? reply = LspTestHarness.ReplyWithId(replies: replies, id: 9);
        Assert.NotNull(@object: reply);
        Assert.True(condition: reply!.Value.GetRawText().Contains(value: doc),
            userMessage: string.Join("\n", replies.Select(r => r.RootElement.GetRawText())));
    }

    [Fact]
    public void MieliaDocument_Analyzes()
    {
        const string sf =
            "module Test/LspMi\n" +
            "import IO/Console\n" +
            "\n" +
            "routine start()\n" +
            "    var x = 41\n" +
            "    var y = x + 1\n" +
            "    show(f\"{y}\")\n" +
            "    return\n";

        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(
            LspTestHarness.DidOpen(uri: "file:///test/Sample.mie", text: sf, languageId: "mielia"),
            LspTestHarness.DocRequest(id: 21, method: "textDocument/documentSymbol", uri: "file:///test/Sample.mie"),
            Exit);

        Assert.NotNull(@object: LspTestHarness.ReplyWithId(replies: replies, id: 21));
    }

    [Fact]
    public void MieliaScript_CallsAreFunctionsAndInferredTypeArgumentsAreHinted()
    {
        const string script =
            "import IO/Console\n" +
            "\n" +
            "show(\"값\" == \"값\")\n" +
            "show(\"값\".count())\n";
        string dir = Directory.CreateTempSubdirectory(prefix: "lsp_script_").FullName;
        string path = Path.Combine(path1: dir, path2: "main.mie");
        File.WriteAllText(path: path, contents: script);
        string uri = new System.Uri(uriString: path).AbsoluteUri;
        IReadOnlyList<JsonDocument> replies;
        try
        {
            replies = LspTestHarness.Run(
                JsonSerializer.Serialize(value: new
                {
                    jsonrpc = "2.0", id = 1, method = "initialize",
                    @params = new { processId = (int?)null, rootUri = new System.Uri(uriString: dir).AbsoluteUri, capabilities = new { } }
                }),
                LspTestHarness.DidOpen(uri: uri, text: script, languageId: "mielia"),
                LspTestHarness.DocRequest(id: 60, method: "textDocument/semanticTokens/full", uri: uri),
                JsonSerializer.Serialize(value: new
                {
                    jsonrpc = "2.0", id = 70, method = "textDocument/inlayHint",
                    @params = new
                    {
                        textDocument = new { uri },
                        range = new { start = new { line = 0, character = 0 }, end = new { line = 50, character = 0 } }
                    }
                }),
                Exit);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }

        List<string> kinds = LspTestHarness.ReplyWithId(replies: replies, id: 1)!.Value.GetProperty(propertyName: "result")
            .GetProperty(propertyName: "capabilities").GetProperty(propertyName: "semanticTokensProvider")
            .GetProperty(propertyName: "legend").GetProperty(propertyName: "tokenTypes").EnumerateArray()
            .Select(selector: k => k.GetString()!).ToList();
        int[] data = LspTestHarness.ReplyWithId(replies: replies, id: 60)!.Value.GetProperty(propertyName: "result")
            .GetProperty(propertyName: "data").EnumerateArray().Select(selector: n => n.GetInt32()).ToArray();
        string[] lines = script.Split(separator: '\n');
        var found = new List<(int Line, string Text, string Kind)>();
        int row = 0;
        int col = 0;
        for (int i = 0; i + 4 < data.Length; i += 5)
        {
            row += data[i];
            col = data[i] == 0 ? col + data[i + 1] : data[i + 1];
            found.Add(item: (row + 1, lines[row].Substring(startIndex: col, length: data[i + 2]), kinds[data[i + 3]]));
        }

        Assert.Equal(expected: "function", actual: found.Single(predicate: t => t.Line == 3 && t.Text == "show").Kind);
        Assert.Equal(expected: "function", actual: found.Single(predicate: t => t.Line == 4 && t.Text == "count").Kind);

        List<string> hints = LspTestHarness.ReplyWithId(replies: replies, id: 70)!.Value.GetProperty(propertyName: "result")
            .EnumerateArray().Select(selector: h => $"{h.GetProperty(propertyName: "position").GetProperty(propertyName: "line").GetInt32() + 1}:{h.GetProperty(propertyName: "label").GetString()}")
            .ToList();
        Assert.Contains(expected: "3:[Bool]", collection: hints);
    }

    // ----- Refactors that cut indentation (code actions of kind refactor.rewrite) -----

    /// <summary>A document from its lines, with the module header and the import every case needs.</summary>
    private static string Doc(params string[] lines)
    {
        return "import IO/Console\n\n" + string.Join(separator: "\n", values: lines) + "\n";
    }

    /// <summary>The refactor actions offered on the line holding <paramref name="needle"/>: each title with the
    /// document as it reads once the action's edit is applied.</summary>
    private static List<(string Title, string Result)> RefactorsAt(string source, string needle)
    {
        // The document is a real file: the types the refactors read come from analyzing its project.
        string dir = Directory.CreateTempSubdirectory(prefix: "lsp_refactor_").FullName;
        string path = Path.Combine(path1: dir, path2: "main.mie");
        File.WriteAllText(path: path, contents: source);
        string refactorUri = new System.Uri(uriString: path).AbsoluteUri;
        (int line, int character) = LspTestHarness.PositionOf(text: source, needle: needle);
        string action = JsonSerializer.Serialize(value: new
        {
            jsonrpc = "2.0",
            id = 77,
            method = "textDocument/codeAction",
            @params = new
            {
                textDocument = new { uri = refactorUri },
                range = new { start = new { line, character }, end = new { line, character } },
                context = new { diagnostics = Array.Empty<object>() }
            }
        });
        IReadOnlyList<JsonDocument> replies = LspTestHarness.Run(
            LspTestHarness.DidOpen(uri: refactorUri, text: source, languageId: "mielia"), action, Exit);
        JsonElement result = LspTestHarness.ReplyWithId(replies: replies, id: 77)!.Value.GetProperty(propertyName: "result");
        var found = new List<(string, string)>();
        foreach (JsonElement item in result.EnumerateArray())
        {
            if (item.GetProperty(propertyName: "kind").GetString() != "refactor.rewrite")
            {
                continue;
            }

            JsonElement edit = item.GetProperty(propertyName: "edit")
                                   .GetProperty(propertyName: "changes")
                                   .GetProperty(propertyName: refactorUri)[index: 0];
            JsonElement range = edit.GetProperty(propertyName: "range");
            int startLine = range.GetProperty(propertyName: "start").GetProperty(propertyName: "line").GetInt32();
            int endLine = range.GetProperty(propertyName: "end").GetProperty(propertyName: "line").GetInt32();
            string[] lines = source.Split(separator: '\n');
            string applied = string.Join(separator: "\n",
                values: lines.Take(count: startLine)
                             .Append(element: edit.GetProperty(propertyName: "newText").GetString()!)
                             .Concat(second: lines.Skip(count: endLine + 1)));
            found.Add(item: (item.GetProperty(propertyName: "title").GetString()!, applied));
        }

        return found;
    }

    private static void AssertOffers(string source, string needle, string title, string expected)
    {
        List<(string Title, string Result)> offered = RefactorsAt(source: source, needle: needle);
        Assert.Contains(expected: title, collection: offered.Select(selector: o => o.Title));
        Assert.Equal(expected: expected, actual: offered.First(predicate: o => o.Title == title).Result);
    }

    private static void AssertNotOffered(string source, string needle, string title)
    {
        Assert.DoesNotContain(expected: title,
            collection: RefactorsAt(source: source, needle: needle).Select(selector: o => o.Title));
    }

    [Fact]
    public void Invert_Integer_FlipsAndKeepsCommentsAndBlankLines()
    {
        AssertOffers(
            source: Doc("routine start()", "    var n: S32 = 5", "    if n < 3", "        show(\"small\")", "",
                "        show(\"still small\")", "    else", "        # the big case", "        show(\"big\")"),
            needle: "if n < 3", title: "Invert 'if'",
            expected: Doc("routine start()", "    var n: S32 = 5", "    if n >= 3", "        # the big case",
                "        show(\"big\")", "    else", "        show(\"small\")", "", "        show(\"still small\")"));
    }

    [Fact]
    public void Invert_Float_IsWrappedInNot()
    {
        AssertOffers(
            source: Doc("routine start()", "    var x: B64 = 0.5", "    if x < 1.0", "        show(\"low\")", "    else",
                "        show(\"high\")"),
            needle: "if x < 1.0", title: "Invert 'if'",
            expected: Doc("routine start()", "    var x: B64 = 0.5", "    if not (x < 1.0)", "        show(\"high\")",
                "    else", "        show(\"low\")"));
    }

    [Fact]
    public void Invert_EqualityAndLogic_SwapAndGoThroughDeMorgan()
    {
        AssertOffers(
            source: Doc("routine start()", "    var a: S32 = 1", "    var b: S32 = 2", "    if a == 1 and b != 2",
                "        show(\"yes\")", "    else", "        show(\"no\")"),
            needle: "if a == 1", title: "Invert 'if'",
            expected: Doc("routine start()", "    var a: S32 = 1", "    var b: S32 = 2", "    if a != 1 or b == 2",
                "        show(\"no\")", "    else", "        show(\"yes\")"));
    }

    [Fact]
    public void Invert_ChainedComparison_IsWrappedInNot()
    {
        AssertOffers(
            source: Doc("routine start()", "    var x: S32 = 5", "    if 0 <= x <= 10", "        show(\"in\")", "    else",
                "        show(\"out\")"),
            needle: "if 0 <= x", title: "Invert 'if'",
            expected: Doc("routine start()", "    var x: S32 = 5", "    if not (0 <= x <= 10)", "        show(\"out\")",
                "    else", "        show(\"in\")"));
    }

    [Fact]
    public void Invert_HasBecomesLacks()
    {
        AssertOffers(
            source: Doc("routine start()", "    var xs: List[S32] = [1, 2]", "    if xs.has(1)", "        show(\"in\")",
                "    else", "        show(\"out\")"),
            needle: "if xs.has", title: "Invert 'if'",
            expected: Doc("routine start()", "    var xs: List[S32] = [1, 2]", "    if xs.lacks(1)", "        show(\"out\")",
                "    else", "        show(\"in\")"));
    }

    [Fact]
    public void Invert_NotOffered_WithoutElseOrWithElseif()
    {
        AssertNotOffered(
            source: Doc("routine start()", "    var n: S32 = 5", "    if n < 3", "        show(\"small\")"),
            needle: "if n < 3", title: "Invert 'if'");
        AssertNotOffered(
            source: Doc("routine start()", "    var n: S32 = 5", "    if n < 3", "        show(\"small\")",
                "    elseif n < 6", "        show(\"mid\")", "    else", "        show(\"big\")"),
            needle: "if n < 3", title: "Invert 'if'");
    }

    [Fact]
    public void Guard_Return_MovesTheBodyOutOneLevel()
    {
        AssertOffers(
            source: Doc("routine start()", "    var n: S32 = 5", "    if n > 2", "        show(\"big\")", "        # note",
                "        show(\"really\")"),
            needle: "if n > 2", title: "Exit early with 'return'",
            expected: Doc("routine start()", "    var n: S32 = 5", "    if n <= 2", "        return", "    show(\"big\")",
                "    # note", "    show(\"really\")"));
    }

    [Fact]
    public void Guard_WithResult_MovesTheFinalReturnIntoTheGuard()
    {
        AssertOffers(
            source: Doc("routine pick(n: S32) -> S32", "    if n > 2", "        show(\"big\")", "        return 1",
                "    return 0", "", "routine start()", "    show(f\"{pick(n: 3)}\")"),
            needle: "if n > 2", title: "Exit early with 'return'",
            expected: Doc("routine pick(n: S32) -> S32", "    if n <= 2", "        return 0", "    show(\"big\")",
                "    return 1", "", "routine start()", "    show(f\"{pick(n: 3)}\")"));
    }

    [Fact]
    public void Guard_Float_IsWrappedInNot()
    {
        AssertOffers(
            source: Doc("routine start()", "    var x: B64 = 0.5", "    if x > 0.25", "        show(\"big\")"),
            needle: "if x > 0.25", title: "Exit early with 'return'",
            expected: Doc("routine start()", "    var x: B64 = 0.5", "    if not (x > 0.25)", "        return",
                "    show(\"big\")"));
    }

    [Fact]
    public void Guard_InLoop_UsesContinue()
    {
        AssertOffers(
            source: Doc("routine start()", "    for i: S32 in 0 til 3", "        if i > 0", "            show(\"positive\")",
                "            show(f\"{i}\")"),
            needle: "if i > 0", title: "Exit early with 'continue'",
            expected: Doc("routine start()", "    for i: S32 in 0 til 3", "        if i <= 0", "            continue",
                "        show(\"positive\")", "        show(f\"{i}\")"));
    }

    [Fact]
    public void Guard_NotOffered_WhereCodeFollowsTheIf()
    {
        AssertNotOffered(
            source: Doc("routine start()", "    var n: S32 = 5", "    if n > 2", "        show(\"big\")",
                "    show(\"after\")"),
            needle: "if n > 2", title: "Exit early with 'return'");
        AssertNotOffered(
            source: Doc("routine start()", "    for i: S32 in 0 til 3", "        if i > 0", "            show(\"positive\")",
                "        show(\"every round\")"),
            needle: "if i > 0", title: "Exit early with 'continue'");
        // A comment between the if and the final return would be dropped.
        AssertNotOffered(
            source: Doc("routine pick(n: S32) -> S32", "    if n > 2", "        return 1", "    # otherwise",
                "    return 0", "", "routine start()", "    show(f\"{pick(n: 3)}\")"),
            needle: "if n > 2", title: "Exit early with 'return'");
    }

    [Fact]
    public void Merge_NestedIf_JoinsWithAnd()
    {
        AssertOffers(
            source: Doc("routine start()", "    var a: S32 = 1", "    var b: S32 = 2", "    if a > 0", "        if b > 1",
                "            show(\"both\")", "            # kept", "            show(\"still\")"),
            needle: "if a > 0", title: "Merge nested 'if'",
            expected: Doc("routine start()", "    var a: S32 = 1", "    var b: S32 = 2", "    if a > 0 and b > 1",
                "        show(\"both\")", "        # kept", "        show(\"still\")"));
    }

    [Fact]
    public void Merge_NotOffered_WithElseOrMoreStatements()
    {
        AssertNotOffered(
            source: Doc("routine start()", "    var a: S32 = 1", "    var b: S32 = 2", "    if a > 0", "        if b > 1",
                "            show(\"both\")", "    else", "        show(\"neither\")"),
            needle: "if a > 0", title: "Merge nested 'if'");
        AssertNotOffered(
            source: Doc("routine start()", "    var a: S32 = 1", "    var b: S32 = 2", "    if a > 0", "        if b > 1",
                "            show(\"both\")", "        show(\"outer\")"),
            needle: "if a > 0", title: "Merge nested 'if'");
    }
}

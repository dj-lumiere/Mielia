using Builder.Frontends;
using Mielia.Lexer;
using TypeModel.Enums;

namespace Mielia;

/// <summary>
/// The Mielia Language Server: the shared engine (<c>Builder.Execution.LspServer</c>) run by <c>mielia lsp</c>,
/// serving <c>.mie</c> files with Mielia's own keywords and names.
/// </summary>
internal sealed class MieliaLanguageServer : LanguageServerProfile
{
    /// <inheritdoc/>
    public override Language Language => Language.Mielia;

    /// <inheritdoc/>
    public override string ServerName => "mielia-lsp";

    /// <inheritdoc/>
    /// <remarks>The language id the Rider plugin registers for highlighting code in hover.</remarks>
    public override string CodeBlockLanguage => "mielia";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Keywords => MieliaLexer.KeywordSpellings;

    /// <inheritdoc/>
    /// <remarks>From the improvement plan: `until` loops, `becomes` in an expression `when`.</remarks>
    public override IReadOnlyList<string> PlannedKeywords { get; } = [];

    /// <inheritdoc/>
    /// <remarks>`suspended` gave way to `agent` at the call.</remarks>
    public override IReadOnlyList<string> RetiredKeywords { get; } =
        ["suspended"];

    /// <inheritdoc/>
    public override string? Format(string text, string fileName)
    {
        return Builder.Formatting.SourceFormatter.Format(source: text, fileName: fileName, language: Language)
                      .Output;
    }
}

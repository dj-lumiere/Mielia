using TypeModel.Enums;
using Builder.Tokenizer;

namespace Mielia.Lexer;

/// <summary>The words Mielia reserves, each with its token type. The shared <see cref="SourceLexer"/> takes this table.</summary>
public static class MieliaKeywords
{
    /// <summary>The keyword table for ordinary source files.</summary>
    public static IReadOnlyDictionary<string, TokenType> Table { get; } = Build();

    /// <summary>Every word this language reserves as a keyword (what the Language Server offers as completions). The retired
    /// <c>for</c> stays reserved but is not offered.</summary>
    public static IReadOnlyList<string> KeywordSpellings { get; } =
        Table.Where(predicate: k => k.Value != TokenType.Each).Select(selector: k => k.Key)
             .Order(comparer: StringComparer.Ordinal).ToList();

    /// <summary>The keyword table for <paramref name="fileName"/>. The standard library's own Mielia files are
    /// builder-internal code, analyzed as Revare: they write the buildtime unrolling (<c>expand</c>) their derives need.
    /// A Mielia program never sees the keyword.</summary>
    public static IReadOnlyDictionary<string, TokenType> TableFor(string fileName)
    {
        if (!IsStandardLibraryFile(fileName: fileName))
        {
            return Table;
        }

        Dictionary<string, TokenType> withExpand = new(collection: Table)
        {
            [key: "expand"] = TokenType.Expand
        };
        return withExpand;
    }

    /// <summary>Whether <paramref name="fileName"/> is inside the standard library directory.</summary>
    private static bool IsStandardLibraryFile(string fileName)
    {
        try
        {
            string root = Path.GetFullPath(path: Builder.Declaration.StdlibLoader.GetDefaultStdlibPath());
            return Path.GetFullPath(path: fileName)
                       .StartsWith(value: root, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static Dictionary<string, TokenType> Build()
    {
        Dictionary<string, TokenType> table = new()
        {
            // Type declarations
            [key: "routine"] = TokenType.Routine,
            [key: "entity"] = TokenType.Entity,
            [key: "record"] = TokenType.Record,
            [key: "choice"] = TokenType.Choice,
            [key: "flags"] = TokenType.Flags,
            [key: "crashable"] = TokenType.Crashable,
            [key: "variant"] = TokenType.Variant,
            [key: "protocol"] = TokenType.Protocol,

            // Variable declarations
            [key: "var"] = TokenType.Var,
            [key: "preset"] = TokenType.Preset,
            [key: "global"] = TokenType.Global,
            [key: "lateinit"] = TokenType.LateInit,

            // Access modifiers
            [key: "secret"] = TokenType.Secret,
            [key: "posted"] = TokenType.Posted,
            [key: "common"] = TokenType.Common,

            // Self references
            [key: "me"] = TokenType.Me,
            [key: "Me"] = TokenType.MyType,

            // Protocol implementation (renamed keywords, old TokenType names)
            [key: "obeys"] = TokenType.Obeys,

            // Control flow
            [key: "if"] = TokenType.If,
            [key: "elseif"] = TokenType.Elseif,
            [key: "else"] = TokenType.Else,
            [key: "then"] = TokenType.Then,
            [key: "with"] = TokenType.With,
            [key: "when"] = TokenType.When,
            [key: "is"] = TokenType.Is,
            [key: "loop"] = TokenType.Loop,
            [key: "for"] = TokenType.For,
            // Reserved: a loop over a range or a collection is written `for` (ME-G229).
            [key: "each"] = TokenType.Each,
            [key: "break"] = TokenType.Break,
            [key: "continue"] = TokenType.Continue,
            [key: "return"] = TokenType.Return,
            [key: "throw"] = TokenType.Throw,
            [key: "todo"] = TokenType.Todo,
            // Recovery prefix keywords: wrap a failable call into a carrier (Optional/Check).
            // Bare words only — `try_foo`/`check_foo` tokenize as single identifiers, untouched.
            [key: "try"] = TokenType.Try,
            [key: "check"] = TokenType.Check,
            // Module system
            [key: "import"] = TokenType.Import,
            [key: "module"] = TokenType.Module,

            // Special keywords
            [key: "using"] = TokenType.Using,
            [key: "as"] = TokenType.As,
            [key: "define"] = TokenType.Define,
            [key: "nothing"] = TokenType.Nothing,
            [key: "given"] = TokenType.Given,
            [key: "in"] = TokenType.In,
            [key: "isnot"] = TokenType.IsNot,
            // Container-first containment (collections AND flags). `notin` is retired — use `lack`.
            [key: "to"] = TokenType.To,
            [key: "til"] = TokenType.Til,
            [key: "by"] = TokenType.By,

            // Logical operators
            [key: "and"] = TokenType.And,
            [key: "or"] = TokenType.Or,
            [key: "not"] = TokenType.Not,

            // Literals
            [key: "true"] = TokenType.True,
            [key: "false"] = TokenType.False,
            [key: "None"] = TokenType.None,
            [key: "none"] = TokenType.NoneValue,

            // Generic constraints
            [key: "needs"] = TokenType.Needs,

            // Associated types
            [key: "relates"] = TokenType.Relates,

            // Unsafe code: the same `danger` blocks and `dangerous` routines as Revare (Hijacked,
            // peek/poke, addresses), so a Mielia library can manage its own memory.
            [key: "danger"] = TokenType.Danger,
            [key: "dangerous"] = TokenType.Dangerous,
            [key: "failable"] = TokenType.Failable,
            [key: "infallible"] = TokenType.Infallible,

            // `needs P everywhere` — the structural-conferral gate of the protocol model (a `needs`-clause
            // modifier, NOT a reflection construct). It lives with `needs`/`obeys`/`protocol`, which MI
            // shares, so it is a SHARED keyword. The derive TEMPLATES it gates (e.g. `routine T.eq` with an
            // `expand` body) are RV stdlib that MI reuses wholesale; authoring such a template needs
            // `expand` (RV-only), but the gate itself and the built-in structural protocols apply to MI
            // types just as well.
            [key: "everywhere"] = TokenType.Everywhere,
            // `until cond` loops while the condition is false, `becomes v` gives a block arm of a value `when` its value.
            [key: "until"] = TokenType.Until,
            [key: "becomes"] = TokenType.Becomes
        };

        return table;
    }
}

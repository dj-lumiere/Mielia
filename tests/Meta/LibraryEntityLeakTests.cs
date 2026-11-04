using System.Text.RegularExpressions;

namespace Mielia.Tests.Meta;

/// <summary>
/// A library entity marked <c>secret</c> (a tree node, a block of places, a gate) is touched only inside the routines of
/// the container that holds it, which run under that container's access lock. Such an entity is made inside the
/// container while the container may already have escaped to another task, so it is never promoted: that is safe only
/// while no routine hands one to a caller. This scans the library for a routine that is not <c>secret</c> and returns a
/// secret entity, and for a member variable that is not <c>secret</c> and has the type of one.
/// </summary>
public sealed class LibraryEntityLeakTests
{
    /// <summary>The ends of a channel are handed out on purpose: the runtime locks the channel itself, so no
    /// promotion or container lock is involved.</summary>
    private static readonly HashSet<string> ChannelEnds = ["Transmitter", "Receiver", "SharedReceiver", "ChannelDrain"];

    private static readonly Regex SecretEntity = new(pattern: @"^secret entity (\w+)", options: RegexOptions.Multiline);

    private static readonly Regex Declaration = new(pattern: @"^(secret |posted )?(entity|record|choice|variant|flags|protocol) ");

    private static readonly Regex Routine = new(pattern: @"^(?<mods>(?:(?:secret|infallible|dangerous|common|posted) |@\w+(?:\([^)]*\))? )*)routine (?<name>[\w.\[\], ]+?)(\[[^\]]*\])?(\.\w+)?\(");

    private static readonly Regex MemberVariable = new(pattern: @"^    (?<name>\w+): (?<type>.+)$");

    [Fact]
    public void NoRoutineHandsASecretEntityToItsCaller()
    {
        string repo = LocateRepoRoot();
        string[] files = LibraryFiles(repo: repo);
        var secrets = new HashSet<string>(comparer: StringComparer.Ordinal);
        foreach (string file in files)
        {
            foreach (Match m in SecretEntity.Matches(input: File.ReadAllText(path: file)))
            {
                if (!ChannelEnds.Contains(item: m.Groups[1].Value))
                {
                    secrets.Add(item: m.Groups[1].Value);
                }
            }
        }

        Assert.NotEmpty(collection: secrets);
        var leaks = new List<string>();
        foreach (string file in files)
        {
            bool inSecretType = false;
            string[] lines = File.ReadAllLines(path: file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (Declaration.Match(input: line) is { Success: true } decl)
                {
                    inSecretType = decl.Groups[1].Value != "";
                    continue;
                }

                if (!inSecretType && MemberVariable.Match(input: line) is { Success: true } member &&
                    !line.TrimStart().StartsWith(value: "secret ") && !line.TrimStart().StartsWith(value: "posted ") &&
                    NamesSecret(text: member.Groups["type"].Value, secrets: secrets) is { } held)
                {
                    leaks.Add(item: $"{Path.GetRelativePath(relativeTo: repo, path: file)}:{i + 1}: member variable of type {held}: {line.Trim()}");
                }

                if (Routine.Match(input: line) is not { Success: true } routine ||
                    routine.Groups["mods"].Value.Contains(value: "secret"))
                {
                    continue;
                }

                // A signature may run over several lines: join them until its parentheses close.
                string signature = line;
                for (int next = i + 1; next < lines.Length && signature.Count(predicate: c => c == '(') > signature.Count(predicate: c => c == ')'); next++)
                {
                    signature += " " + lines[next].Trim();
                }

                int arrow = signature.LastIndexOf(value: "->", comparisonType: StringComparison.Ordinal);
                if (arrow < 0)
                {
                    continue;
                }

                // A creator of a secret type is not reachable by name outside its module, so it hands nothing out.
                if (NamesSecret(text: signature[(arrow + 2)..], secrets: secrets) is { } returned &&
                    !secrets.Contains(item: Regex.Match(input: routine.Groups["name"].Value, pattern: @"^\w+").Value))
                {
                    leaks.Add(item: $"{Path.GetRelativePath(relativeTo: repo, path: file)}:{i + 1}: returns {returned}: {line.Trim()}");
                }
            }
        }

        Assert.True(condition: leaks.Count == 0,
            userMessage: "A routine hands a secret library entity to its caller, which no promotion covers:\n" +
                         string.Join(separator: "\n", values: leaks));
    }

    private static string? NamesSecret(string text, HashSet<string> secrets)
    {
        foreach (Match word in Regex.Matches(input: text, pattern: @"\b\w+\b"))
        {
            if (secrets.Contains(item: word.Value))
            {
                return word.Value;
            }
        }

        return null;
    }

    private static string[] LibraryFiles(string repo)
    {
        string[] roots = [Path.Combine(path1: repo, path2: "Standard"), Path.Combine(paths: [repo, "..", "Revare", "Standard"])];
        return roots.Where(predicate: Directory.Exists)
                    .SelectMany(selector: root => Directory.EnumerateFiles(path: root, searchPattern: "*.*", searchOption: SearchOption.AllDirectories))
                    .Where(predicate: f => f.EndsWith(value: ".mi") || f.EndsWith(value: ".rr"))
                    .ToArray();
    }

    private static string LocateRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(value: dir))
        {
            if (File.Exists(path: Path.Combine(path1: dir, path2: "Mielia.csproj")))
            {
                return dir;
            }

            string? parent = Path.GetDirectoryName(path: dir);
            if (parent == null || parent == dir)
            {
                break;
            }

            dir = parent;
        }

        throw new InvalidOperationException(message: "Could not find the Mielia repository root.");
    }
}

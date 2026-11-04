using Builder;

namespace Mielia.Tests.BuildSystem;

/// <summary>
/// The <c>[debug] sanitize</c> key of config.toml and the IR mark that makes ThreadSanitizer instrument a routine.
/// </summary>
public sealed class SanitizeTests
{
    private static ProjectManifest Load(string debugSection)
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: "sf_sanitize_" + Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: dir);
        try
        {
            string toml = Path.Combine(path1: dir, path2: "config.toml");
            File.WriteAllText(path: toml,
                contents: "[package]\nname = \"t\"\n\n[target]\nexecutable = \"main\"\nmode = \"debug\"\n\n" +
                          debugSection);
            return ManifestLoader.Load(tomlPath: toml, resolveExecutable: false);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void SanitizeThread_IsRead()
    {
        Assert.Equal(expected: "thread", actual: Load(debugSection: "[debug]\nsanitize = \"thread\"\n").Debug.Sanitize);
    }

    [Fact]
    public void SanitizeIsOffByDefaultAndForNone()
    {
        Assert.Null(@object: Load(debugSection: "").Debug.Sanitize);
        Assert.Null(@object: Load(debugSection: "[debug]\nsanitize = \"none\"\n").Debug.Sanitize);
    }

    [Fact]
    public void UnknownSanitizer_IsABuildError()
    {
        var error = Assert.Throws<InvalidOperationException>(
            testCode: () => Load(debugSection: "[debug]\nsanitize = \"address\"\n"));
        Assert.Contains(expectedSubstring: "thread", actualString: error.Message);
    }

    [Fact]
    public void MarkForThreadSanitizer_PutsTheAttributeAfterTheAttributesAndBeforeTheMetadata()
    {
        string ir = string.Join(separator: "\n",
            "define internal void @a(ptr sret(%T) align 8 %r) #0 !dbg !1 {",
            "define linkonce_odr hidden i1 @b(i32 %x) nounwind comdat !dbg !2 {",
            "define i32 @c() #3 {",
            "declare void @d(ptr)",
            "  ret void",
            "}",
            "");
        string marked = NativeToolchain.MarkForThreadSanitizer(ir: ir);
        Assert.Contains(expectedSubstring: "@a(ptr sret(%T) align 8 %r) #0 sanitize_thread !dbg !1 {", actualString: marked);
        Assert.Contains(expectedSubstring: "@b(i32 %x) nounwind sanitize_thread comdat !dbg !2 {", actualString: marked);
        Assert.Contains(expectedSubstring: "@c() #3 sanitize_thread {", actualString: marked);
        Assert.Contains(expectedSubstring: "declare void @d(ptr)\n", actualString: marked);
        Assert.Equal(expected: 3, actual: marked.Split(separator: "sanitize_thread").Length - 1);
        Assert.EndsWith(expectedEndString: "}\n", actualString: marked);
    }
}

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="branding/mielia-dark.svg">
    <img src="branding/mielia.svg" alt="Mielia logo" width="112">
  </picture>
</p>

<h1 align="center">Mielia</h1>

<p align="center"><strong>Sweet to write, sure to run.</strong></p>

<p align="center">
  <img src="https://img.shields.io/badge/version-0.5.0-informational.svg" alt="Version 0.5.0">
  <img src="https://img.shields.io/badge/status-early%20alpha-orange.svg" alt="Status: early alpha">
  <img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License: MIT">
</p>

<p align="center">
  <a href="https://mielia.lumi-dev.xyz/">Documentation</a> ·
  <a href="#quick-start">Quick start</a> ·
  <a href="MIELIA-FOR-AI.md">Reference for AI assistants</a> ·
  <a href="CHANGELOG.md">Changelog</a>
</p>

Mielia (`.mie`) is a natively compiled language for building applications without managing memory by
hand. It is the sibling of [Revare](https://github.com/dj-lumiere/Revare): the same grammar,
the same standard library, the same loud failures, and every entity shared for you. A file
can be just its statements, and running it is one command: `mielia run hello.mie`.

```mielia
import IO/Console

entity Point
    x: Int
    y: Int

global MOVES: Int = 0

routine nudge(p: Point)
    p.x = p.x + 1
    MOVES = MOVES + 1

var p = Point(x: 3, y: 4)
var q = p                   # both names refer to the same Point
nudge(p: q)
show(f"p.x = {p.x}")        # 4
show(f"moves = {MOVES}")    # 1

show(0.1 + 0.2 == 0.3)      # true: bare numbers are Int and Decimal
show(99999999999999999999 * 99999999999999999999)
```

> **Early alpha.** Mielia 0.5 is the current version, numbered together with Revare. Programs build and run on Windows, Linux, and
> macOS, and every commit runs the Mielia test suite and its end-to-end programs in CI, several of
> them locked to produce the same output as their Revare twins. APIs will change, and you will
> find bugs.

## What we are trying to keep

- **No ceremony.** Nothing you write is there only because the language asks for it.
- **Code is the expression of intent.** What you write says what you mean.
- **What should work, works.** Code that looks like it should work does, and it does what it looks like it does.
- **One acceptable way.** There is a way that works for everyone, even if it isn't everyone's favorite.
- **Nothing goes wrong silently.** A failure is loud, a hang is reported, and no work is left running that nobody will
  ever check.

And for Mielia, the joy of having made something.

## What it is like

**Copy or share, decided once.** A value is either copied or shared, and the type says which when you
declare it: a `record` is copied, an `entity` is shared. `var q = p` gives
you a second name for the same entity, with nothing to write for the sharing. Its memory is freed once nothing
uses it anymore, entities that point at each other included, and tasks can share it safely. An entity that may be missing is written `Point?` and must be checked against `None`
before you use it.

**Numbers are exact by default.** A bare `42` is an arbitrary-precision `Int` and a bare `3.14`
is a base-10 `Decimal` with 34 significant digits, so `0.1 + 0.2 == 0.3` holds, and whole numbers never overflow. A number gets a
fixed-width type (`S32`, `U64`, `B64`, …) when its declaration names one: `var x: U8 = 5`.

**A file can be just its statements.** Top-level statements run top to bottom with no
`routine start()`, and `module` is optional. A routine ends where its body ends, with no closing `return`.

**`global` is module state that tasks can share.** `global NAME: Type = value` declares module-level
mutable state. Globals initialize once, in dependency order, and every global is thread-safe: a
single-statement update such as `COUNT = COUNT + 1` is atomic across parallel tasks.

**Failure is loud, and recovery takes one keyword.** A `throw` passes the failure to the caller,
and nothing marks a routine that can fail or a call to it. A bare call that fails
crashes with a message (exit status 82). `try` or `check` in front of the call recovers it as `T?` (`Optional[T]`)
or `Check[T]` (which keeps the error). Out of memory and runaway recursion are fatal and not recoverable.

**Nothing unsafe is reachable.** `danger` blocks, `dangerous` routines, raw handles, and `steal` are
not part of Mielia. Changing a list while a `for` loop walks it is stopped with a clear error: at
build time when you do it directly, at run time when a called routine does it.

## Quick start

### 1. Install

Download the package for your platform from the
[releases page](https://github.com/dj-lumiere/Mielia/releases). Mielia and Revare ship in one
package, so the file name starts with `revare-`:

| Platform              | File                                 |
|-----------------------|--------------------------------------|
| Windows x64           | `revare-v<version>-win-x64.zip`    |
| Linux x64             | `revare-v<version>-linux-x64.tar.gz` |
| macOS (Apple Silicon) | `revare-v<version>-osx-arm64.tar.gz` |

The package is self-contained: the builder, the standard library, the runtime, and the LLVM
toolchain are all inside it. Unpack it anywhere and run the installer from that folder:

```bat
:: Windows: adds the folder to your user PATH
install.cmd
```

```bash
# Linux / macOS: links the commands into ~/.local/bin
./install.sh
```

Open a new terminal and check that it works:

```bash
mielia version
```

The short alias `mi` works as well.

> **Linux:** linking needs the C library's development files. Most machines have them. Otherwise
> install `libc6-dev` (Debian/Ubuntu) or `glibc-devel` (Fedora) once.
>
> **macOS:** linking uses Apple's Command Line Tools. Run `xcode-select --install` once if you have
> never built anything on this Mac. This alpha is not notarized, and `install.sh` clears the
> Gatekeeper quarantine on the unpacked folder for you.

### 2. Hello, world

```mielia
# hello.mie
import IO/Console

show("Hello from Mielia!")
```

```bash
mielia run hello.mie
```

`run` builds the file and runs it. `mielia build hello.mie` stops after building
and leaves `hello.exe` next to the source.

### Using an AI assistant?

Mielia is not in any model's training data yet, so assistants tend to guess Python-flavored syntax
that does not build. Point yours at [`MIELIA-FOR-AI.md`](MIELIA-FOR-AI.md) together with
[`REVARE-FOR-AI.md`](https://github.com/dj-lumiere/Revare/blob/master/REVARE-FOR-AI.md)
(the shared grammar). Both ship in every release package. The CI-verified programs in
[`tests/Fixtures/StdlibMi/`](tests/Fixtures/StdlibMi/) make good starting points.

## Command line

```
mielia new [folder]                       Start a project (config.toml, main.mie, editor files)
mielia migrate [--check] <paths>...       Move sources from the previous release to this one
mielia check [--json] [entry-file]        Check the program without building it
mielia check <folder>...                  Check every source in the folders: canonical layout, hints and notes
mielia build [entry-file]                 Build a native executable for this OS (no run)
mielia run [entry-file]                   Build the program and run it
mielia codegen [entry-file] [out.ll]      Write the program's LLVM IR without building it
mielia test <dir-or-file>...              Run test programs against their expected output
mielia format [--check] <file-or-dir>...  Format sources in place
mielia docs <source-dir> <output-dir>     Write the API reference pages of a library
mielia lsp                                Run the language server (for an editor)
mielia help | version
```

There are no build flags. A solution is configured in a `config.toml` manifest, the same one
Revare uses (see the
[Revare README](https://github.com/dj-lumiere/Revare#command-line)), and the standard library
is found next to the `mielia` executable, so a `.mie` file anywhere on disk builds.

## Mixing with Revare

A `.mie` file can `import` a Revare module and use its types and routines directly. An entity
that comes from Revare keeps Revare's rules for sharing: you can use it freely as a local or
a field, and to pass one across a Mielia routine's signature you share it through a `Retained[T]`. This is how a Mielia program reaches lower-level code written in Revare.

## Not there yet

The REPL and hot reload are planned and not implemented (runtime reflection is `import ObjectHacker`). Async networking is
missing in both languages. See the status note at the end of [`MIELIA-FOR-AI.md`](MIELIA-FOR-AI.md)
for the current state.

## Documentation

The documentation lives at [mielia.lumi-dev.xyz](https://mielia.lumi-dev.xyz/):

- [Hello World](https://mielia.lumi-dev.xyz/Hello-World) ·
  [Entities](https://mielia.lumi-dev.xyz/Entities) ·
  [Numeric Types](https://mielia.lumi-dev.xyz/Numeric-Types)
- [Error Handling](https://mielia.lumi-dev.xyz/Error-Handling) ·
  [Collections](https://mielia.lumi-dev.xyz/Collections) ·
  [Concurrency Model](https://mielia.lumi-dev.xyz/Concurrency-Model)
- [Choosing a Language](https://mielia.lumi-dev.xyz/Choosing-Language) ·
  [Revare Interop](https://mielia.lumi-dev.xyz/Revare-Interop) ·
  [Design Philosophy](https://mielia.lumi-dev.xyz/Design-Philosophy)

## This repository

```
Mielia/
├── src/
│   ├── Lexer/             # The Mielia lexer
│   ├── Passes/            # Mielia-only lowering: shared entities, module globals, loop shape checks
│   └── MieliaCli.cs       # The `mielia` command line
├── tests/                 # Mielia.Tests: unit tests and end-to-end fixtures
│   └── Fixtures/StdlibMi/ #   programs with expected output
├── Mielia.tmbundle/       # TextMate grammar
└── MIELIA-FOR-AI.md       # Reference for AI assistants
```

The builder itself is [Anvila](https://github.com/dj-lumiere/Anvila), the standard library lives in
[Revare](https://github.com/dj-lumiere/Revare/tree/master/Standard), and the shared library
under it is [Ingrid](https://github.com/dj-lumiere/Ingrid).

## Building from source

You only need this to work on the builder or the standard library. Mielia is built from five
repositories checked out side by side: the builder core
([Anvila](https://github.com/dj-lumiere/Anvila)), the shared library
([Ingrid](https://github.com/dj-lumiere/Ingrid)), [Revare](https://github.com/dj-lumiere/Revare)
(whose standard library Mielia uses), this repository, and
[Lapila](https://github.com/dj-lumiere/Lapila), whose builder compiles part of the native runtime.

You need the .NET 10 SDK, LLVM 22 (`clang` and `opt` on `PATH`), CMake 3.20+, and Ninja on Windows.

```bash
mkdir LumiFoundry && cd LumiFoundry
git clone https://github.com/dj-lumiere/Anvila.git
git clone https://github.com/dj-lumiere/Ingrid.git
git clone https://github.com/dj-lumiere/Revare.git
git clone https://github.com/dj-lumiere/Mielia.git
git clone https://github.com/dj-lumiere/Lapila.git

dotnet build Mielia/Mielia.csproj                # also builds the native runtime
dotnet test Mielia/tests/Mielia.Tests.csproj     # optional
```

The built command is `Mielia/bin/Debug/net10.0/Mielia` (`Mielia.exe` on Windows).

## Contributing

Bug reports, feature suggestions, documentation fixes, and code are all welcome. Report bugs at
[github.com/dj-lumiere/Mielia/issues](https://github.com/dj-lumiere/Mielia/issues).

## License

MIT. See [`LICENSE`](LICENSE).

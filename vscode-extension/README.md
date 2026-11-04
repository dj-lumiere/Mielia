# Mielia for VS Code

Mielia (`.mi`) in VS Code: highlighting from `../Mielia.tmbundle`, comment toggling and bracket pairing,
and the Mielia language server (`Mielia lsp`): errors as you type, semantic colors, hover, completion, go to
definition, rename, find usages and formatting.

## Which server runs

The setting `mielia.serverPath` sets it: `Mielia.dll` (run with `dotnet`) or a `Mielia` executable. Left empty, the
extension uses the dev build in the workspace, `<workspace>/Mielia/bin/Debug/net10.0/Mielia.dll`, else `Mielia` on the
PATH. The server runs from a copy of its build folder in the extension's storage, so it never holds the files a rebuild
overwrites, and a rebuild restarts it. The command "Mielia: Restart the language server" restarts it by hand.

## Build

Needs Node.js. The grammar, the language configuration and the icon are copied in from `../Mielia.tmbundle` and
`../rider-plugin`, their one source.

```
npm install
npm run package          # -> dist/mielia.vsix
code --install-extension dist/mielia.vsix
```

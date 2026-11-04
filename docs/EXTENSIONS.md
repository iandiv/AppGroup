# AppGroup extensions

Extensions add optional features to AppGroup. Each one is a separate executable that
AppGroup discovers and runs on demand, so an extension crash can't take AppGroup down
and nothing extra is loaded unless the user installs it.

Bundled extensions (in `Extensions/`):

| Extension | Capability | Adds |
|---|---|---|
| `AppGroup.Extension.Folders` | `folderItems` | "Add folder" in the group editor and folder drag-and-drop. Clicking a folder in a popup lists its contents; without the extension a folder item just opens in File Explorer. |
| `AppGroup.Extension.Separators` | `taskbarSeparator` | "Add Taskbar Separator" in the main window's menu. Creates a pinnable shortcut with a thin-line icon and its own AppUserModelID; clicking it does nothing. |

## Installing

Copy the extension's publish output (its exe plus `extension.json`) into its own folder under any of:

- `<AppGroup install dir>\Extensions\<name>\`
- `<AppGroup data dir>\Extensions\<name>\` (`config\Extensions` in portable mode)
- `%LOCALAPPDATA%\AppGroup\Extensions\<name>\`

Settings > Extensions lists what was found and has a button that opens the last folder.

Build them with:

```
dotnet publish Extensions/AppGroup.Extension.Folders -c Release -r win-x64 --self-contained false
dotnet publish Extensions/AppGroup.Extension.Separators -c Release -r win-x64 --self-contained false
```

## Manifest (`extension.json`)

```json
{
  "id": "appgroup.folders",
  "name": "Folders",
  "description": "Add folders to a group and browse their contents from the popup.",
  "version": "1.0.0",
  "protocol": 1,
  "executable": "AppGroup.Extension.Folders.exe",
  "capabilities": [ "folderItems" ]
}
```

`executable` must be a relative path inside the extension folder. Manifests with a `protocol`
other than the one AppGroup supports (currently `1`) are ignored.

## Protocol v1

AppGroup runs the executable with a command and options as arguments, with no window, and
reads a single JSON object from stdout. The call times out after 10 seconds.

Success: `{ "ok": true, ... }`. Failure: `{ "ok": false, "error": "message" }`.

| Capability | Command | Result |
|---|---|---|
| `folderItems` | `list --path <folder> [--max <n>]` | `"items": [ { "name", "path", "isFolder", "icon" } ]` (`icon` is a PNG path or null) |
| `taskbarSeparator` | `create [--style line\|blank]` | `"path": "<created .lnk>"` |

The separator exe also accepts `--taskbar-separator`, which pinned separators launch with; it exits immediately.

## Microsoft Store (future)

Discovery goes through `IExtensionSource` in `AppGroup/Extensions/ExtensionManager.cs`.
The Store build can add a source backed by Windows App Extensions (`AppExtensionCatalog`,
declared with `uap3:AppExtension` in each extension's package manifest) that returns the same
`ExtensionInfo` and talks the same protocol. Store policy doesn't allow downloading code at
runtime, so Store extensions must ship as their own packages.

![AppGroup](https://github.com/user-attachments/assets/169e1383-fe84-4f6b-997e-75ee218abe0c)

# App Group

Organize, customize, and launch your apps from the Windows taskbar. Create groups with custom icons, nest subgroups, and pin everything with a single drag.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [How to Use](#how-to-use)
- [Creating UWP Shortcuts](#creating-uwp-shortcuts)
- [Support](#support)
- [License](#license)

---

## Features

### Group Management
- Create, edit, delete, and duplicate groups
- **Subgroups** — nest groups inside groups, infinitely deep
- Reorder groups and apps via drag & drop
- Drag a group icon to the Desktop or Taskbar to pin it
- Jumplist actions: **Edit this Group** and **Launch All**

### Appearance & Customization
- **Icon styles**: Single icon or auto-generated Grid icon from app icons
- **Layouts**: Default or Card
- Accent-colored group backgrounds
- Show or hide group name headers (position: Top or Bottom)
- Adjust grid column count
- Dark Mode and Light Mode — configurable independently for the main window and popup
- Use `.exe` files as custom icons
- Custom tooltips and per-app launch arguments
- Toggleable window and content animations

### App & Shortcut Support
- UWP and PWA apps (via shortcuts)
- `.lnk` shortcuts without the arrow overlay (where supported)
- Steam shortcuts (`.url`)
- Run apps as Administrator
- App labels with configurable position (Bottom or Right)

### Import & Export
- `.agz` (AppGroupZip) format for import, export, backup, and sharing
- Import from **TaskbarGroups** — migrate existing groups directly
- Preview and selectively import groups — check or uncheck before confirming
- Imported groups are always appended, never overwriting existing ones

### System & Performance
- System tray support
- Hybrid in-memory and persistent icon cache for faster load times
- Auto-refreshes taskbar icons when group icons change
- Grayscale taskbar icon option
- Supports all taskbar positions: Top, Bottom, Left, Right
- Run at startup
- Built-in update notifier

---

## Installation

App Group is available in four variants:

| Variant | Includes Installer | Includes .NET 8 |
|---|---|---|
| Setup | ✅ | ❌ |
| Setup (Bundled) | ✅ | ✅ |
| Portable | ❌ | ❌ |
| Portable (Bundled) | ❌ | ✅ |

**Not sure which to pick?** Download **Setup (Bundled)** — it includes everything you need.

1. Go to the [Releases page](https://github.com/iandiv/AppGroup/releases) and download your preferred variant.
2. Run the `.exe` installer (Setup), or extract the `.zip` and run `AppGroup.exe` (Portable).

---

## Screenshots & Demo

<img src="https://github.com/user-attachments/assets/39d02528-0cda-43f3-abc4-7a2567140c58" width="300"> <img src="https://github.com/user-attachments/assets/73703278-b4c8-4b93-a4cb-5c8cf49ae2a8" width="300"> <img src="https://github.com/user-attachments/assets/4ef0825d-506e-49be-9f1b-5f66faf4ad8f" width="300">

https://github.com/user-attachments/assets/6d37560f-16ea-45a9-b8b2-9d94bced0ff2

---

## How to Use

### 1. Create a Group
Click **+** in the main window and enter a group name.

### 2. Add Apps
Click **+** inside the group or drag and drop apps into it.
For UWP apps (e.g. Calculator, Settings), see [Creating UWP Shortcuts](#creating-uwp-shortcuts).

### 3. Customize the Group
- **Header**: Enable *Show Header* and set its position (Top or Bottom)
- **Layout**: Choose Default or Card
- **Icon style**: *Regular* (pick any icon file) or *Grid* (auto-generated from app icons)
- **Grid columns**: Adjust for your preferred layout
- **Labels**: Enable *Show Labels* and set position (Bottom or Right)

### 4. Pin a Group to the Taskbar

**Option A — Drag and drop:**
Drag the group icon directly onto the taskbar.

<img width="500" alt="Drag group to taskbar" src="https://github.com/user-attachments/assets/b54ac465-929d-46d1-98c4-2681aaed5995" />

**Option B — Via the app menu:**
Click the **⋮** menu → **Open File Location**, then right-click the shortcut → **Pin to Taskbar**.

### 5. Use Subgroups
Add an existing group as an item inside another group. Subgroups can be nested without limit.

### 6. Import Groups

Click **⋮** in the main window → **Import** → choose **`.agz`** or **TaskbarGroups**.

A preview will show all groups — check or uncheck individual groups before confirming. Selected groups are appended to your existing setup.

---

## Creating UWP Shortcuts

UWP apps (like Calculator or Settings) don't appear as regular files. To add them to a group:

1. Press `Win + R`, type `shell:AppsFolder`, and press Enter — this opens a folder with all installed apps (Win32 and UWP).
2. Find the app, right-click it, and select **Create shortcut**.
3. If prompted, click **Yes** — the shortcut will be placed on your Desktop.
4. Drag that shortcut into your App Group.

---

## Support

App Group is actively maintained. If you find it useful, consider supporting development:

**[☕ Donate on Ko-fi](https://ko-fi.com/iandiv/tip)**

<a href="https://ko-fi.com/iandiv/tip" target="_blank">
  <img src="https://github.com/user-attachments/assets/2e1376d4-d3a5-4ac4-95fc-e5aa512a1704" width="400" alt="Ko-fi donation">
</a>

---

## License

MIT — see [LICENSE](LICENSE) for details.

> [!NOTE]
> Please ensure you're downloading from this official repository.

> [!TIP]
> **AppGroup v1.6.0 VirusTotal results:** All four release variants were scanned and showed no detections at the time of checking.
>
> - [Portable (.zip)](https://www.virustotal.com/gui/file/5b8272f6deed5b14b52192e090a1cd8c244f22b5e4b7849b3ada39582c555bf9)
> - [Portable (Bundled .zip)](https://www.virustotal.com/gui/file/9a98a8c7101eff049f177e75ff7459421fd7b8458fab5ea4524441c53e0e8685)
> - [Setup (.exe)](https://www.virustotal.com/gui/file/3388223e02bf595ac417dba011ccd4ab555b5bbcb058bc3f506e0a56e757a5a4)
> - [Setup (Bundled .exe)](https://www.virustotal.com/gui/file/a6cb1a9e88be7350702eeab3c77ae473827e5216aec817611bc0b11387a35845)

---

<img width="256" alt="appgroup high color" src="https://github.com/user-attachments/assets/f952f7fa-2c12-41e1-ba7c-b3b6ea47c586" />


# App Group

Organize, customize, and launch your apps from the Windows taskbar. Create groups with custom icons, nest subgroups, and pin everything with a single drag.

[![Latest Release](https://img.shields.io/github/v/release/iandiv/AppGroup?style=for-the-badge&label=Latest&color=0078D4)](https://github.com/iandiv/AppGroup/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/iandiv/AppGroup/total?style=for-the-badge&label=Downloads&color=2EA44F)](https://github.com/iandiv/AppGroup/releases)
[![Stars](https://img.shields.io/github/stars/iandiv/AppGroup?style=for-the-badge&label=Stars&color=F5B301)](https://github.com/iandiv/AppGroup/stargazers)
[![License](https://img.shields.io/github/license/iandiv/AppGroup?style=for-the-badge&color=6E7781)](LICENSE)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)

**[⬇ Download the latest release](https://github.com/iandiv/AppGroup/releases/latest)**

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [How to Use](#how-to-use)
- [Support](#support)
- [License](#license)

---

## Features

### Group Management
- Create, edit, delete, and duplicate groups
- **Subgroups** — nest groups inside groups, infinitely deep
- Reorder groups and apps via drag & drop, or sort groups alphabetically (disables manual reordering)
- Drag a group icon to the Desktop or Taskbar to pin it
- Jumplist actions: **Edit this Group** and **Launch All**
- **Launch All** toggle in Settings

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
- Drag regular and UWP apps directly into a group from the Windows Start Menu or `shell:AppsFolder`
- PWA apps (via shortcuts)
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
- Portable version stores groups in its own directory instead of `AppData\Local\AppGroup\Groups`
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

> **Note:** Since v1.6.0, the portable versions store groups in their own folder. If your existing groups don't appear after upgrading, export them as an `.agz` file from the old version and import them into the portable one.

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
Click **+** inside the group, or drag and drop apps into it.

Regular and UWP apps (e.g. Calculator, Settings) can be dragged directly from either of these:

- **The Windows Start Menu**
- **`shell:AppsFolder`** — press `Win + R`, type `shell:AppsFolder`, and press Enter to open a folder with all installed apps (Win32 and UWP)

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

#### Import from `.agz`

1. Click **⋮** in the main window → **Import** → choose **`.agz`**.
2. A preview shows all groups. Check or uncheck individual groups before confirming.
3. Selected groups are appended to your existing setup.

#### Import from `TaskbarGroups`

1. Click ⋮ in the main window → **Import** → choose **TaskbarGroups**.
2. In the dialog, select the folder that contains `config`, then click **Select Folder**.
   - **TaskbarGroupsFluent:** type `%APPDATA%\Jack Schierbeck` into the address bar and press Enter. Then click the `taskbar-groups` folder.
   - **TaskbarGroups:** locate your TaskbarGroups folder.
3. A preview shows all groups. Pick the ones you want to include and confirm. They're added to your existing groups.

> **Note:** The import automatically finds the `config` folder. Don't select the `config` folder itself, since that isn't supported yet. Select its parent folder.
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

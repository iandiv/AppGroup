> [!NOTE]
> Download only from this official repository: [github.com/iandiv/AppGroup](https://github.com/iandiv/AppGroup). 

<div align="center">

<img width="128" alt="AppGroup logo: Windows taskbar app group launcher" src="https://github.com/user-attachments/assets/f952f7fa-2c12-41e1-ba7c-b3b6ea47c586" />

# AppGroup: Windows Taskbar App Groups & Launcher

**Organize your apps into groups you can pin to the Windows taskbar, then open any group from one icon.**

[![Latest Release](https://img.shields.io/github/v/release/iandiv/AppGroup?style=for-the-badge&label=Latest&color=0078D4)](https://github.com/iandiv/AppGroup/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/iandiv/AppGroup/total?style=for-the-badge&label=Downloads&color=2EA44F)](https://github.com/iandiv/AppGroup/releases)
[![Stars](https://img.shields.io/github/stars/iandiv/AppGroup?style=for-the-badge&label=Stars&color=F5B301)](https://github.com/iandiv/AppGroup/stargazers)
[![License: MIT](https://img.shields.io/github/license/iandiv/AppGroup?style=for-the-badge&color=6E7781)](LICENSE)
![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)

**[⬇ Download (Setup, Bundled)](https://github.com/iandiv/AppGroup/releases/latest)** · [Quick start](#quick-start) · [Demo](#see-it-in-action) · [FAQ](#faq) · [Get help](#help-and-support)

</div>

AppGroup is a free, open-source (MIT) taskbar organizer and app launcher for Windows. It keeps the familiar Windows taskbar workflow: you pin a shortcut, and AppGroup supplies the popup and the group's contents.

## Why AppGroup?

A crowded taskbar slows you down. With AppGroup you can, for example:

- pin **one group for work** (editor, browser, chat, notes),
- **one for games** (launchers and Steam shortcuts),
- **one for creative apps**,

then open the group you need from a single taskbar icon instead of hunting through a long row of icons. A group can also open all of its apps at once with **Launch All**.

## Quick start

1. **[Download Setup (Bundled)](https://github.com/iandiv/AppGroup/releases/latest)** and run the installer. It includes .NET 8, so there is nothing else to install. (Prefer no installer? Use **Portable (Bundled)** from the same page.)
2. **Create a group and add apps.** Click **+** in AppGroup, name the group, then click **+** inside it or drag apps in from the Start Menu or `shell:AppsFolder`.
3. **Drag the group icon onto the taskbar.** Click it to open the popup and launch an app.

## How a group works

AppGroup creates a **shortcut for each group**. When you pin that shortcut, Windows keeps managing the taskbar as usual; AppGroup draws the popup and holds the group's contents (apps, shortcuts, and subgroups). AppGroup does not replace the taskbar and does not create real folders on it.

## See it in action

<p align="center">
  <img src="https://github.com/user-attachments/assets/39d02528-0cda-43f3-abc4-7a2567140c58" width="300" alt="AppGroup main window listing the user's app groups">
  <img src="https://github.com/user-attachments/assets/73703278-b4c8-4b93-a4cb-5c8cf49ae2a8" width="300" alt="AppGroup group editor showing a group's apps and icon options">
  <img src="https://github.com/user-attachments/assets/4ef0825d-506e-49be-9f1b-5f66faf4ad8f" width="300" alt="AppGroup popup showing the apps in a group opened from the taskbar">
</p>
<p align="center"><em>Left to right: the group list, the group editor, and a group's popup.</em></p>

https://github.com/user-attachments/assets/6d37560f-16ea-45a9-b8b2-9d94bced0ff2

*Demo video: AppGroup in use.*

<h3 align="center">Pin a group to the taskbar or desktop</h3>

<p align="center">
  <img width="270" alt="Animation of dragging an AppGroup group icon onto the Windows taskbar to pin it" src="https://github.com/user-attachments/assets/b54ac465-929d-46d1-98c4-2681aaed5995" />
  <img width="350" alt="Animation of dragging an AppGroup group icon onto the Windows desktop to create a shortcut" src="https://github.com/user-attachments/assets/a8994310-76e4-44fd-ad1e-d8ff3e9f2521" />
</p>
<p align="center"><em>Drag a group icon onto the taskbar (left) or the desktop (right).</em></p>

<h3 align="center">Subgroups</h3>

<p align="center">
  <img width="550" alt="An AppGroup popup with a subgroup opened inside a group" src="https://github.com/user-attachments/assets/90d80744-6a6e-4a99-8fe8-a5469e401f39" />
</p>
<p align="center"><em>Open a group inside a group.</em></p>

<h3 align="center">System tray</h3>

<p align="center">
  <img width="250" alt="AppGroup running in the Windows system tray" src="https://github.com/user-attachments/assets/8319d486-e1c5-470d-8b8e-1c49f4fb2f92" />
</p>
<p align="center"><em>AppGroup can live in the system tray.</em></p>

<h3 align="center">Layouts and header position</h3>

<p align="center">
  <img width="250" alt="AppGroup Default layout with the group name header at the bottom" src="https://github.com/user-attachments/assets/4129bc50-9bf6-4e26-9282-bd1d32461632" />
  <img width="250" alt="AppGroup Default layout with the group name header at the top" src="https://github.com/user-attachments/assets/85db9020-94ac-491b-b7e3-d18429db97b1" />
  <img width="250" alt="AppGroup Card layout with the group name header at the bottom" src="https://github.com/user-attachments/assets/f9f0cfb8-b5cd-4274-b498-0c61527511da" />
  <img width="250" alt="AppGroup Card layout with the group name header at the top" src="https://github.com/user-attachments/assets/b0afdd7f-4bfe-4c17-a031-f6721f86c020" />
</p>
<p align="center"><em>Default and Card layouts, each with the header at the top or bottom.</em></p>

<h3 align="center">Import with preview</h3>

https://github.com/user-attachments/assets/ed518124-53a7-40c4-97cb-42d7ac331d63

<p align="center"><em>Preview groups before importing from an <code>.agz</code> file or TaskbarGroups.</em></p>

## Features

### Groups
- Create, edit, delete, and duplicate groups
- **Subgroups:** add a group as an item inside another group; it opens as its own popup
- Reorder groups and apps by drag and drop, or sort groups alphabetically (disables manual reordering)
- Pin a group shortcut by dragging its icon to the taskbar or desktop
- Jumplist actions: **Edit this Group** and **Launch All** (toggle in Settings)

### Appearance
- **Icon styles:** single icon, or an auto-generated grid icon from the apps' icons
- **Layouts:** Default or Card; adjustable grid columns
- Accent-colored group backgrounds; show or hide group name headers (Top or Bottom)
- Dark and Light mode, set independently for the main window and popup
- `.exe` files as custom icons; custom tooltips; per-app launch arguments
- App labels (Bottom or Right); toggleable window and content animations

### Apps and shortcuts
- Regular and UWP apps, dragged from the Start Menu or `shell:AppsFolder`
- PWA apps (via shortcuts) and Steam shortcuts (`.url`)
- `.lnk` shortcuts without the arrow overlay (where supported)
- Run apps as Administrator

### Import, export, and system
- `.agz` (AppGroupZip) files for import, export, backup, and sharing; preview and select groups before importing; imports are appended, never overwriting
- Import from **TaskbarGroups** ([migration steps](#coming-from-taskbargroups-or-taskbargroupsfluent))
- System tray support, run at startup, grayscale taskbar icon option
- Supports taskbars positioned at the top, bottom, left, or right
- Icon cache for faster loading; taskbar icons refresh when group icons change
- Built-in update notifier

## Installation

**Requires:** Windows 10 version 1809 (build 17763) or later, including Windows 11. **Built with:** WinUI 3 and .NET 8.

| Variant | Installer | Includes .NET 8 | Best for |
|---|---|---|---|
| **Setup (Bundled)** | ✅ | ✅ | Most users (recommended) |
| Setup | ✅ | ❌ | Machines that already have .NET 8 |
| **Portable (Bundled)** | ❌ | ✅ | Running from a folder, no install |
| Portable | ❌ | ❌ | Portable use with .NET 8 already installed |

Download from the [latest release](https://github.com/iandiv/AppGroup/releases/latest). **Setup:** run the `.exe`. **Portable:** extract the `.zip` and run `AppGroup.exe`.

> [!NOTE]
> Since v1.6.0, portable versions store groups in their own folder (standard installs use `AppData\Local\AppGroup\Groups`). If your groups don't appear after upgrading, export them as an `.agz` file from the old version and import it into the portable one.

## How to Use

### Add apps
Click **+** inside a group, or drag and drop. Regular and UWP apps can be dragged from:
- **The Windows Start Menu**
- **`shell:AppsFolder`:** press `Win + R`, type `shell:AppsFolder`, and press Enter to see all installed Win32 and UWP apps

### Customize a group
- **Header:** enable *Show Header*, choose Top or Bottom
- **Layout:** Default or Card
- **Icon style:** *Regular* (pick any icon file) or *Grid* (auto-generated from app icons)
- **Grid columns** and **Labels** (*Show Labels*, Bottom or Right)

### Pin a group to the taskbar
- **Option A:** drag the group icon onto the taskbar.
- **Option B:** click **⋮** → **Open File Location**, then right-click the shortcut → **Pin to Taskbar**.

### Use subgroups
Add an existing group as an item inside another group. Selecting it opens its own popup.

### Back up or share groups (`.agz`)
1. Click **⋮** → **Export** to save groups as an `.agz` file.
2. To restore: **⋮** → **Import** → **`.agz`**, check or uncheck groups in the preview, and confirm. Selected groups are appended to your setup.

## Coming from TaskbarGroups or TaskbarGroupsFluent?

You can migrate your existing groups:

1. Click **⋮** → **Import** → **TaskbarGroups**.
2. Select the folder that **contains** `config`, then click **Select Folder**.
   - **TaskbarGroupsFluent:** paste `%APPDATA%\Jack Schierbeck` into the address bar, press Enter, then select the `taskbar-groups` folder.
   - **TaskbarGroups:** locate your TaskbarGroups folder.
3. Pick the groups to include and confirm. They are appended to your existing groups.

> [!NOTE]
> Select the parent folder, not the `config` folder itself. Selecting `config` directly isn't supported yet.

## Security and VirusTotal reports

These VirusTotal reports cover the **v1.6.0** release files only. At the time each file was scanned, no security vendor flagged it. VirusTotal results are snapshots: they are not a guarantee or certification of safety, some engines may time out or fail to analyze a file, and they do not apply to later versions.

- [Portable (.zip)](https://www.virustotal.com/gui/file/5b8272f6deed5b14b52192e090a1cd8c244f22b5e4b7849b3ada39582c555bf9)
- [Portable (Bundled .zip)](https://www.virustotal.com/gui/file/9a98a8c7101eff049f177e75ff7459421fd7b8458fab5ea4524441c53e0e8685)
- [Setup (.exe)](https://www.virustotal.com/gui/file/3388223e02bf595ac417dba011ccd4ab555b5bbcb058bc3f506e0a56e757a5a4)
- [Setup (Bundled .exe)](https://www.virustotal.com/gui/file/a6cb1a9e88be7350702eeab3c77ae473827e5216aec817611bc0b11387a35845)

The source code is public. Download only from this repository's [Releases page](https://github.com/iandiv/AppGroup/releases).

## FAQ

### How do I group apps on the Windows taskbar?
Create a group in AppGroup, add your apps, then drag the group icon onto the taskbar. Clicking the pinned shortcut opens a popup where you can launch any app in the group.

### Does AppGroup modify the Windows taskbar?
No. AppGroup creates a group shortcut that you pin. Windows still manages the taskbar; AppGroup provides the popup and group contents.

### Is AppGroup free?
Yes. It is free and open source under the [MIT License](LICENSE).

### Which Windows versions are supported?
Windows 10 version 1809 (build 17763) or later, including Windows 11. Check the [release notes](https://github.com/iandiv/AppGroup/releases) for requirements specific to a release.

### Can I add UWP / Microsoft Store apps like Calculator or Settings?
Yes. Drag them from the Start Menu or `shell:AppsFolder` into a group.

### Can I launch all apps in a group at once?
Yes, with the **Launch All** jumplist action (it can be toggled in Settings).

### Can I put one group inside another?
Yes. Add an existing group as an item in another group; it opens as its own popup.

### Can I migrate from TaskbarGroups?
Yes. See [Coming from TaskbarGroups or TaskbarGroupsFluent?](#coming-from-taskbargroups-or-taskbargroupsfluent).

### How do I back up or share my groups?
Export them as an `.agz` (AppGroupZip) file and import it on another PC.

### Is there a portable version?
Yes: Portable and Portable (Bundled) `.zip` builds. Since v1.6.0 they keep groups in their own folder.

### Is AppGroup safe?
The source code is public, and the v1.6.0 release files had no vendor detections on VirusTotal when scanned. That is a snapshot for those specific files, not proof of safety. See [Security and VirusTotal reports](#security-and-virustotal-reports), and download only from this repository.

## Help and support

- **Bug or feature request:** [GitHub Issues](https://github.com/iandiv/AppGroup/issues)
- **Questions and discussion:** [GitHub Discussions](https://github.com/iandiv/AppGroup/discussions)
- **What changed in each version:** [Release notes](https://github.com/iandiv/AppGroup/releases)
- **Support development:** [Donate on Ko-fi](https://ko-fi.com/iandiv/tip). Starring the repo also helps others find it. ⭐

<a href="https://ko-fi.com/iandiv/tip" target="_blank">
  <img src="https://github.com/user-attachments/assets/2e1376d4-d3a5-4ac4-95fc-e5aa512a1704" width="400" alt="Support AppGroup on Ko-fi">
</a>

## License

MIT. See [LICENSE](LICENSE).

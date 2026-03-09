<div align="center">

# PresenterShield

**A stealthy privacy overlay for live presentations, screen sharing, and streaming.**

[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-blue.svg)]()
[![Built With](https://img.shields.io/badge/Built%20With-C%23%20%7C%20WPF%20%7C%20MVVM-purple.svg)]()

</div>

---

## 📖 The Story Behind PresenterShield

A lot of times I wanted to demonstrate as a TA some code walkthroughs, or I had a presentation in PDF, and I couldn't easily have my presenter notes open without exposing them to the audience when sharing my entire screen.

When presenting via HDMI, screen share (Zoom, Teams, Meet), or a projector, you usually have two less-than-ideal options: share everything (and let the audience see your private notes) or use complex setups like OBS and manually switch sources mid-presentation. Neither is acceptable for a natural teaching or presenting workflow.

So, I built **PresenterShield**.

## 🚀 Core Concept

PresenterShield creates two parallel "views" of your desktop from a single screen:

- **Public View** (What the audience sees): Your full desktop minus any windows you've specifically marked as private.
- **Private View** (What you see): Everything is visible to you, but your private windows are overlaid with a configurable opacity. You never lose context of what's underneath them.

The key distinction from a simple "hide this window" toggle: **private windows remain fully interactive and focus-stable at all times.** You can type into them, scroll them, click through them, and they never drop behind other windows when you interact with something else. You work naturally. You Alt+Tab freely. Your private windows follow you around as persistent overlays. **The audience never sees them.**

---

## 🛠️ Architecture & Under the Hood

The project is built using:
- **C#** and **WPF** for the desktop UI.
- The **MVVM** pattern leveraging `CommunityToolkit.Mvvm` for clean separation between `ViewModels` (e.g., `MainViewModel`) and the underlying `Services`.
- Direct interaction with the **Win32 API** to achieve the shielding effect.

### 🪄 The Win32 Magic Working Together

These three mechanisms need to work together without breaking each other:

1. Windows must be **invisible to all capture pipelines** (HDMI out, screen share, OBS).
2. Windows must remain **fully interactive** regardless of where the user clicks.
3. Windows must **never drop behind** other windows or flash on the public view during focus changes.

A naive approach would solve one and break the others. PresenterShield keeps all three intact by combining the following mechanisms:

**1. Hiding from Capture via Shellcode Injection**

`SetWindowDisplayAffinity` with the `WDA_EXCLUDEFROMCAPTURE` flag makes a window invisible to any capture mechanism. However, Windows strictly requires this API to be called *from within the process that owns the window*. To bypass this, `ShellcodeInjector.cs` injects x64 assembler shellcode directly into the target process's memory via `VirtualAllocEx` and `CreateRemoteThread`, forcing the target application to call the API on itself.

**2. Hiding from the Taskbar and Alt-Tab**

By swapping the target window's extended styles (`GWL_EXSTYLE`), removing `WS_EX_APPWINDOW` and applying `WS_EX_TOOLWINDOW`, the window is physically removed from the taskbar and Alt-Tab switcher. This prevents it from accidentally surfacing on the public view during rapid task switching.

**3. The Always-On-Top Interactive Overlay**

`WS_EX_LAYERED` + `SetLayeredWindowAttributes` controls the window's opacity, turning your private notes into a floating, semi-transparent overlay. `SetWindowPos` with `HWND_TOPMOST` ensures it stays above all other windows regardless of focus changes, so interacting with your editor, browser, or any other window never pushes the overlay behind. It stays put, stays interactive, and stays invisible to the audience.

*(All changes are fully reversible when you stop the session.)*

### Why This Is Non-Trivial

Most attempts at this kind of overlay fail in at least one of these ways:
- The window drops behind when the user clicks elsewhere (Z-order lost on focus change).
- The window becomes non-interactive once made topmost or transparent.
- The window briefly flashes on the public view during Alt-Tab or taskbar interactions.

Preventing all three simultaneously requires precise coordination of Z-order management, focus event handling, and the capture pipeline, effectively building a lightweight custom window compositor on top of the standard Windows desktop.

---

## 🎯 Use Cases

Beyond my initial pain point of TA sessions, PresenterShield is incredibly versatile for:

*   **Live Coding & Teaching:** Keep your reference implementations, terminal cheatsheets, or lesson plans open right next to your code editor.
*   **Corporate Presentations:** Read from a hidden script or keep an eye on incoming Slack messages while projecting financial reports.
*   **Content Creators & Streamers:** Keep your stream chat, OBS controls, or sensitive dashboard information visible to you on a single-monitor setup without it ever broadcasting.
*   **Remote Meetings:** Cross-reference confidential internal documents while screen-sharing collaborative whiteboards or Jira boards to clients.

---

## ⚙️ How to Build and Run

Due to the application's reliance on memory injection (`ShellcodeInjector.cs`), there are specific requirements to build and run it successfully.

### Prerequisites
- Windows 10 or later.
- .NET Framework / .NET Core (depending on your build configuration).
- **Target Platform:** Must be built and run as **x64**. The remote shellcode execution is specifically tailored for 64-bit processes. 32-bit (`x86`) processes are currently bypassed.

### Building
1. Clone the repository.
2. Open `PresenterShield.sln` in Visual Studio.
3. Ensure the active solution platform is set to **x64**.
4. Build and Run.

*(Depending on your system configuration, running the application may require **Administrator privileges** to successfully inject into other running processes.)*
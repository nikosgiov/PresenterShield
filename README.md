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

You work naturally. You Alt+Tab freely. Your private windows follow you around as overlays. **The audience never sees them.**

---

## 🛠️ Architecture & Under the Hood

The project is built using:
- **C#** and **WPF** for the desktop UI.
- The **MVVM** pattern leveraging `CommunityToolkit.Mvvm` for clean separation between `ViewModels` (e.g., `MainViewModel`) and the underlying `Services`.
- Direct interacting with the **Win32 API** to achieve the shielding effect.

### 🪄 The Win32 Magic Working Together

To achieve complete invisibility on the public view but full interactivity on the private view, PresenterShield utilizes three mechanisms simultaneously:

**1. Hiding from Capture via Shellcode Injection**
We utilize `SetWindowDisplayAffinity` with the `WDA_EXCLUDEFROMCAPTURE` flag to make windows invisible to any capture mechanism (HDMI out, screen share, OBS, Teams, etc.).
However, Windows strictly requires this API to be called *from within the process that owns the window*. To bypass this limitation, `ShellcodeInjector.cs` injects x64 assembler shellcode directly into the target process's memory space via `VirtualAllocEx` and `CreateRemoteThread`, forcing the target application to call the API on itself. 

**2. Hiding from the Taskbar and Alt-Tab**
By seamlessly swapping the target window's extended styles (`GWL_EXSTYLE`), we remove `WS_EX_APPWINDOW` and apply `WS_EX_TOOLWINDOW`. This physically hides the app's trace from the taskbar and the Alt-Tab menu so it doesn't accidentally flash during rapid task switching.

**3. The Always-On-Top Opacity Overlay**
We apply the `WS_EX_LAYERED` extended style to configure window transparency (`SetLayeredWindowAttributes`), and use `SetWindowPos` to push the window to `HWND_TOPMOST`. This turns your secret notes into a floating, slightly transparent overlay that only you can see, allowing you to click through or reference code underneath it.

*(All changes are fully reversible when you stop the session.)*

---

## 🎯 Use Cases

Beyond my initial pain point of TA sessions, PresenterShield is incredibly versatile for:

*   **Live Coding & Teaching:** Keep your reference implementations, terminal cheatsheets, or lesson plans open right next to your code editor.
*   **Corporate Presentations:** Read from a hidden script or keep an eye on incoming Slack messages while projecting financial reports.
*   **Content Creators & Streamers:** Keep your stream chat, OBS controls, or sensitive dashboard information visible to you on a single-monitor setup without it ever broadcasting.
*   **Remote Meetings:** Cross-reference confidential internal documents while screen-sharing collaborative whiteboards or Jira boards to clients.

---

## ⚙️ How to Build and Run

Due to the nature of the application and its reliance on memory injection (`ShellcodeInjector.cs`), there are specific requirements to build and run the project successfully.

### Prerequisites
- Windows 10 or later.
- .NET Framework / .NET Core (depending on your build configuration).
- **Target Platform:** Must be built and run as **x64**. The remote shellcode execution is specifically tailored for 64-bit processes (`CreateRemoteThread`, x64 assembly bytecode). 32-bit (`x86`) processes are currently bypassed.

### Building
1. Clone the repository.
2. Open `PresenterShield.sln` in Visual Studio.
3. Ensure the active solution platform is set to **x64**.
4. Build and Run.

*(Depending on your system configurations, running the application may require **Administrator privileges** to successfully inject into other running processes.)*

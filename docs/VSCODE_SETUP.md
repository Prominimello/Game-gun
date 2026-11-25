# Setting up Breachpoint in VS Code on a PC

A step-by-step guide to get the C# source running in Visual Studio Code on Windows (macOS and Linux notes at the end), working with GitHub, and building the Android APK yourself.

---

## 1. Install the tools (one time, about 15 minutes)

| Tool | Why | Where |
|---|---|---|
| **.NET 8 SDK** | Compiles and runs the C# code | https://dotnet.microsoft.com/download/dotnet/8.0 → "SDK x64" installer |
| **Visual Studio Code** | The editor | https://code.visualstudio.com |
| **Git** | Download the code and push changes to GitHub | https://git-scm.com/download/win (accept the defaults) |
| *(Android only)* **Android Studio** | Provides the Android SDK and JDK used to build the APK | https://developer.android.com/studio |

Check that .NET installed correctly. Open **Command Prompt** or **PowerShell** and run:

```bash
dotnet --version
```

It should print `8.0.x` or newer.

## 2. Get the code

```bash
git clone https://github.com/Prominimello/Game-gun.git
```

```bash
cd Game-gun
```

```bash
git checkout claude/bold-goldberg-fib5tw
```

## 3. Open it in VS Code

1. Start VS Code → **File → Open Folder…** → pick the `Game-gun` folder.
2. When the pop-up says **"Do you want to install the recommended extensions?"**, click **Install**. This installs **C# Dev Kit** and the C# language support from `.vscode/extensions.json`.
3. Wait for the status bar to finish "Restoring" and "Loading projects". The first time takes a minute because NuGet downloads MonoGame and the other packages.
4. If C# Dev Kit asks you to sign in with a Microsoft account, that is optional for personal use.

## 4. Play and debug

- Press **F5** (or **Run → Start Debugging**) and choose **"Play Breachpoint (Desktop)"**. The game window opens. Breakpoints work in every `.cs` file.
- **Ctrl+Shift+B** builds without running.
- **Terminal → Run Task…** lists everything else:

| Task | What it does |
|---|---|
| `run` | Build and launch the game |
| `test` | Run the 60 unit tests (map generator, simulation, saves) |
| `autotest` | The game plays through every screen by itself and saves screenshots to `./screenshots` |
| `publish: windows` | Makes a stand-alone Windows build in `./artifacts/win-x64` (copy that folder anywhere; no .NET needed to run it) |
| `publish: linux` | Same for Linux |
| `android: …` | See section 6 |

Or from a terminal in VS Code (**Ctrl+`**):

```bash
dotnet run --project src/Breachpoint.Desktop
```

```bash
dotnet test
```

## 5. Project layout

```
Breachpoint.sln                    ← open this solution (VS Code does it automatically)
src/
  Breachpoint.Core/                ← pure game rules, no graphics: data, saves, map generator, simulation, AI
  Breachpoint.Game/                ← shared MonoGame code: rendering, procedural art, synthesized audio, UI, screens
  Breachpoint.Desktop/             ← Windows/Linux/macOS launcher (Program.cs)
  Breachpoint.Android/             ← Android launcher (Activity1.cs, manifest, icons)
tests/Breachpoint.Core.Tests/      ← xUnit tests
.github/workflows/build.yml        ← GitHub Actions: tests, desktop zips, Android APK, Releases
.vscode/                           ← launch configs, tasks, recommended extensions
```

Good places to start editing:

| Want to change… | Look in |
|---|---|
| Weapons, attachments, gear, prices | `src/Breachpoint.Core/Data/GameData.cs` |
| Campaign missions, organizations, enemy types, difficulty | `src/Breachpoint.Core/Data/CampaignData.cs` |
| Map layouts | `src/Breachpoint.Core/Map/MapGenerator.cs` |
| Squad orders, enemy AI, shooting | `src/Breachpoint.Core/Sim/Mission*.cs` |
| How things look | `src/Breachpoint.Game/Gfx/Art.cs`, `MapRenderer.cs` |
| Sounds | `src/Breachpoint.Game/Audio/SoundSystem.cs` |
| Screens / menus | `src/Breachpoint.Game/Screens/` |

Your save game lives in `%LOCALAPPDATA%\Breachpoint\profile.json` on Windows. Delete it to start fresh.

## 6. Build the Android APK yourself (optional)

GitHub builds the APK automatically (section 7), so you only need this to build locally or test on a phone over USB.

1. Install **Android Studio**, open it once and let it download the SDK. In **More Actions → SDK Manager**, make sure **Android 14 (API 34)** is installed.
2. Install the .NET Android workload. Use an **Administrator** terminal on Windows:
   ```bash
   dotnet workload install android
   ```
3. Build:
   ```bash
   dotnet publish src/Breachpoint.Android -c Release -f net8.0-android -o artifacts/android
   ```
   The installable file is `artifacts/android/com.breachpoint.taskforce-Signed.apk`. It is signed with a debug key.
4. To install straight onto your phone: enable **Developer options → USB debugging** on the phone, plug it in, then run the VS Code task **`android: install on phone`**.

If the build can't find the SDK, add `-p:AndroidSdkDirectory="C:\Users\<you>\AppData\Local\Android\Sdk"` to the command.

## 7. Working with GitHub

The everyday loop in VS Code's **Source Control** panel (**Ctrl+Shift+G**):

1. Edit code.
2. In Source Control, type a message and click **Commit**.
3. Click **Sync Changes** (or run `git push`).

On every push, **GitHub Actions** (`.github/workflows/build.yml`) automatically:

- runs the unit tests,
- plays through the game headless and saves screenshots (download them under the run's **Artifacts**),
- builds **Windows** and **Linux** zips,
- builds a signed **Android APK**,
- publishes everything on the repository's **Releases** page.

Watch progress on GitHub under the **Actions** tab, or install the recommended *GitHub Actions* extension to see runs inside VS Code.

**Permanent Android signing key (recommended).** Android only installs an update over an existing install if both are signed with the same key. Make a key once:

```bash
keytool -genkeypair -keystore breachpoint.jks -alias breachpoint -keyalg RSA -keysize 2048 -validity 10000
```

Then in GitHub → **Settings → Secrets and variables → Actions**, add these four secrets:

- `BP_KEYSTORE_BASE64`: the file encoded as base64. On Windows PowerShell: `[Convert]::ToBase64String([IO.File]::ReadAllBytes("breachpoint.jks"))`
- `BP_KEYSTORE_PASSWORD` and `BP_KEY_PASSWORD`: the passwords you chose
- `BP_KEY_ALIAS`: `breachpoint`

Keep the `.jks` file private. It is listed in `.gitignore` so it never gets committed.

## 8. Troubleshooting

| Problem | Fix |
|---|---|
| Red squiggles everywhere / "project not loaded" | Command Palette (**Ctrl+Shift+P**) → **.NET: Restart Language Server**, or run `dotnet restore` |
| `dotnet` not recognized | Restart VS Code (or the PC) after installing the SDK |
| Black window or crash on start (old PCs / VMs) | Update graphics drivers; the game needs OpenGL 3.0 |
| No voice callouts on Windows | Voice uses Windows' built-in speech (PowerShell `System.Speech`). Toggle it in **Settings → Voice callouts**. |
| Android build: "workload not installed" | `dotnet workload install android` from an admin terminal |

## macOS / Linux notes

- Install the .NET 8 SDK for your OS. On Ubuntu: `sudo apt install dotnet-sdk-8.0`.
- Everything else is identical. On Linux, voice callouts use `spd-say` if it is installed (`sudo apt install speech-dispatcher`).

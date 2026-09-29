# Microsoft Store Sürümü — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Amaç:** Uygulamayı Microsoft Store'da yayınlanabilir bir MSIX paketine çevirmek; GitHub exe'si aynı koddan aynen sürer.

**Mimari:** Tek exe açılışta paketli olup olmadığını (`GetCurrentPackageFullName`) anlar. Paketliyken "Windows ile başlat" Run kaydı yerine manifestteki StartupTask ile çalışır ve ikon sabitleme yapılmaz; ikonun yeri yalnızca okunur. `build.ps1 -Msix` exe'yi, Store görsellerini, manifesti ve `resources.pri`'yi bir klasörde toplayıp `makeappx` ile paketler.

**Teknoloji:** C# 5 / .NET Framework 4.8 WinForms (Windows'la gelen `csc.exe`), WinRT (`%WINDIR%\System32\WinMetadata`), MSIX (Windows SDK 10.0.26100: `makeappx`, `makepri`), PowerShell 5.1.

**Spec:** `docs/superpowers/specs/2026-09-29-microsoft-store-design.md`

## Genel Kısıtlar

- Kaynaklar **C# 5**: `$""`, `?.`, `nameof`, `=>` üyeler, exception filter, otomatik özellik başlatıcı yok.
- `build.ps1` yalnızca **ASCII** (PowerShell 5.1 onu ANSI okur); Türkçe metinler `.cs`, `.xml`, `.md` dosyalarında (UTF-8).
- Exe'yi derlemek **Windows SDK istemez**: WinRT `%WINDIR%\System32\WinMetadata` üzerinden; SDK yalnızca `-Msix` adımında gerekir.
- "Windows ile başlat" **yalnızca menüden elle** açılır; manifestte `StartupTask Enabled="false"`.
- Paketli sürüm `HKCU\Control Panel\NotifyIconSettings`'e **hiç yazmaz**.
- Paket sürümü `AssemblyFileVersion` ile aynı, son hanesi `0`; `MinVersion="10.0.19041.0"`; dil `tr-TR`; mimari `x64`.
- Menü yalnızca sağ tıkla açılır; arayüz metinleri Türkçe.
- Build komutu (repo kökünde): `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1` — testler derlemenin ilk adımıdır, başarısız test exe üretimini durdurur.
- Commit mesajları `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` ile biter.

## İnceleme Odağı

1. **Kullanıcı başlangıç görevini Ayarlar'dan kapattıysa** menüye tıklayınca sessizce işaretsiz kalmamalı; Ayarlar → Başlangıç sayfası açılıp neden anlatılmalı → Task 2'de `FromTaskState` eşleme testleri.
2. **Hem GitHub exe'si hem Store sürümü kuruluysa** ikincisi açılınca ikinci bir ikon çıkmamalı (ortak mutex) → Task 6, Adım 6.
3. **Manifest ile kod ayrışırsa** (TaskId, görsel adları) başlangıç sessizce bozulur ya da ikonlar eksik çıkar → Task 4'te manifest–kod tutarlılık testleri.
4. **SDK'sız bir makinede kaynaktan derleme** (`-Msix` olmadan) çalışmaya devam etmeli → Task 5, Adım 4.
5. **Paketli ilk açılışta Explorer ikonu henüz kaydetmemişse** ipucu metni gösterilmeli (kayıt yok = gizli) → Task 3 birim testi + Task 6, Adım 4.

---

### Task 1: Store paketinde çalıştığını algılama

**Files:**
- Create: `AppPackage.cs`
- Modify: `Tests.cs` (testler), `build.ps1` (kaynak listeleri)

**Interfaces:**
- Produces: `AppPackage.IsPackaged` → `bool` (static, bir kez hesaplanır)

- [ ] **Step 1: Failing test** — `Tests.cs` içinde `// Values must actually be random` bloğundan önce ekle:

```csharp
            // Tests.exe always runs as a plain exe, never from the Store package
            Expect("Paketsiz calisma algilaniyor", !AppPackage.IsPackaged);
```

`build.ps1`'de iki listeye `AppPackage.cs` ekle:

```powershell
$appSources = 'Generators.cs', 'TrayApp.cs', 'TrayPin.cs', 'AppPackage.cs', 'Toast.cs', 'AppIcon.cs', 'AssemblyInfo.cs' | ForEach-Object { Join-Path $root $_ }
$testSources = 'Generators.cs', 'TrayPin.cs', 'AppPackage.cs', 'Tests.cs' | ForEach-Object { Join-Path $root $_ }
```

- [ ] **Step 2: Run** `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1` — Expected: FAIL, derleme hatası (`AppPackage.cs` yok).

- [ ] **Step 3: Implement** — `AppPackage.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace TestVeriUretici
{
    /// <summary>Whether this process runs from the Microsoft Store package (has package identity) or as the plain GitHub exe.</summary>
    internal static class AppPackage
    {
        private const int AppModelErrorNoPackage = 15700;
        private static readonly bool isPackaged = Detect();

        public static bool IsPackaged
        {
            get { return isPackaged; }
        }

        private static bool Detect()
        {
            int length = 0;
            return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);
    }
}
```

- [ ] **Step 4: Run** build — Expected: `Tum testler gecti.` ve `Hazir: ...TestVeriUretici.exe`.

- [ ] **Step 5: Commit**

```bash
git add AppPackage.cs Tests.cs build.ps1
git commit -m "Store paketinde çalıştığını algıla" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Windows ile başlat — Store'da StartupTask

**Files:**
- Create: `AutoStart.cs` (`TrayApp.cs`'deki `AutoStart` sınıfı buraya taşınır ve genişler)
- Modify: `TrayApp.cs` (`AutoStart` sınıfı silinir; `Program.Main`, `TrayContext`), `Tests.cs`, `build.ps1` (WinRT referansları, `AutoStart.cs`)

**Interfaces:**
- Consumes: `AppPackage.IsPackaged`
- Produces:
  - `enum AutoStartState { Disabled, Enabled, DisabledByUser }`
  - `AutoStart.Flag` (`"--autostart"`), `AutoStart.TaskId` (`"TestVeriUretici"`, internal const)
  - `bool AutoStart.WasStartedAtSignIn(string[] args)`
  - `Task<AutoStartState> AutoStart.GetStateAsync()`, `Task<AutoStartState> AutoStart.SetEnabledAsync(bool enabled)`
  - `void AutoStart.OpenStartupSettings()`, `AutoStartState AutoStart.FromTaskState(StartupTaskState state)` (internal)

- [ ] **Step 1: Failing tests** — `Tests.cs` başına `using Microsoft.Win32;` ve `using Windows.ApplicationModel;` ekle; Task 1 testinin altına:

```csharp
            Expect("--autostart oturum acilisi sayiliyor", AutoStart.WasStartedAtSignIn(new[] { AutoStart.Flag }));
            Expect("Argumansiz acilis elle acilis sayiliyor", !AutoStart.WasStartedAtSignIn(new string[0]));
            Expect("Windows ile baslat durumu Run kaydini yansitiyor",
                (AutoStart.GetStateAsync().Result == AutoStartState.Enabled) == RunEntryExists());
            // A task switched off in Settings must stay distinguishable from a plain "off" the menu may flip back on
            Expect("Ayarlar'dan kapatilan gorev ayri durum", AutoStart.FromTaskState(StartupTaskState.DisabledByUser) == AutoStartState.DisabledByUser);
            Expect("Ilkeyle acik gorev acik sayiliyor", AutoStart.FromTaskState(StartupTaskState.EnabledByPolicy) == AutoStartState.Enabled);
            Expect("Ilkeyle kapali gorev kapali sayiliyor", AutoStart.FromTaskState(StartupTaskState.DisabledByPolicy) == AutoStartState.Disabled);
```

`Tests` sınıfına yardımcı (salt okunur; testler kullanıcının Run kaydına yazmaz):

```csharp
        private static bool RunEntryExists()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key != null && key.GetValue("TestVeriUretici") != null;
        }
```

`build.ps1`'de derleyici ayarlarını ve listeleri güncelle:

```powershell
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
# WinRT (StartupTask for the Store package) through the metadata every Windows 10/11 has, so no SDK is needed
$winmd = Join-Path $env:WINDIR 'System32\WinMetadata'
$common = '/nologo', '/codepage:65001', '/optimize+', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
    "/r:$winmd\Windows.ApplicationModel.winmd", "/r:$winmd\Windows.Foundation.winmd", "/r:$fw\System.Runtime.dll"
$appSources = 'Generators.cs', 'TrayApp.cs', 'TrayPin.cs', 'AutoStart.cs', 'AppPackage.cs', 'Toast.cs', 'AppIcon.cs', 'AssemblyInfo.cs' | ForEach-Object { Join-Path $root $_ }
$testSources = 'Generators.cs', 'TrayPin.cs', 'AutoStart.cs', 'AppPackage.cs', 'Tests.cs' | ForEach-Object { Join-Path $root $_ }
```

- [ ] **Step 2: Run** build — Expected: FAIL, derleme hatası (`AutoStartState`, `FromTaskState` tanımsız).

- [ ] **Step 3: Implement** — `AutoStart.cs`:

```csharp
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;

namespace TestVeriUretici
{
    internal enum AutoStartState { Disabled, Enabled, DisabledByUser }

    /// <summary>
    /// "Windows ile başlat", only ever turned on from the menu. The GitHub exe uses a per-user Run entry. The Store
    /// package cannot (its registry writes go to a private copy Windows never reads), so it uses the StartupTask in
    /// store\AppxManifest.xml, which the user also sees and controls in Settings > Apps > Startup.
    /// </summary>
    internal static class AutoStart
    {
        /// <summary>Added to the Run entry so a start at sign-in shows no notice.</summary>
        public const string Flag = "--autostart";

        /// <summary>uap5:StartupTask TaskId in store\AppxManifest.xml.</summary>
        internal const string TaskId = "TestVeriUretici";

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TestVeriUretici";

        public static bool WasStartedAtSignIn(string[] args)
        {
            return AppPackage.IsPackaged ? ActivatedByStartupTask() : Array.IndexOf(args, Flag) >= 0;
        }

        public static Task<AutoStartState> GetStateAsync()
        {
            return AppPackage.IsPackaged ? GetTaskStateAsync() : Task.FromResult(RunEntryState());
        }

        public static Task<AutoStartState> SetEnabledAsync(bool enabled)
        {
            if (AppPackage.IsPackaged) return SetTaskEnabledAsync(enabled);
            SetRunEntry(enabled);
            return Task.FromResult(RunEntryState());
        }

        /// <summary>Settings > Apps > Startup: the only place a startup task the user switched off can be switched back on.</summary>
        public static void OpenStartupSettings()
        {
            Process.Start("ms-settings:startupapps");
        }

        internal static AutoStartState FromTaskState(StartupTaskState state)
        {
            switch (state)
            {
                case StartupTaskState.Enabled:
                case StartupTaskState.EnabledByPolicy:
                    return AutoStartState.Enabled;
                case StartupTaskState.DisabledByUser:
                    return AutoStartState.DisabledByUser;
                default:
                    return AutoStartState.Disabled;
            }
        }

        private static AutoStartState RunEntryState()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(ValueName) != null ? AutoStartState.Enabled : AutoStartState.Disabled;
        }

        private static void SetRunEntry(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" " + Flag);
                else key.DeleteValue(ValueName, false);
            }
        }

        // The WinRT calls below only work with package identity; unpackaged, Windows rejects them

        private static async Task<AutoStartState> GetTaskStateAsync()
        {
            StartupTask task = await Await(StartupTask.GetAsync(TaskId));
            return FromTaskState(task.State);
        }

        private static async Task<AutoStartState> SetTaskEnabledAsync(bool enabled)
        {
            StartupTask task = await Await(StartupTask.GetAsync(TaskId));
            if (!enabled)
            {
                task.Disable();
                return FromTaskState(task.State);
            }
            // Packaged desktop apps get no consent dialog; a task the user switched off in Settings stays off
            return FromTaskState(await Await(task.RequestEnableAsync()));
        }

        private static bool ActivatedByStartupTask()
        {
            try
            {
                IActivatedEventArgs activation = AppInstance.GetActivatedEventArgs();
                return activation != null && activation.Kind == ActivationKind.StartupTask;
            }
            catch (Exception)
            {
                return false; // no activation info: treat it as an ordinary start and show the notice
            }
        }

        // The Windows-bundled compiler only sees the per-namespace metadata in System32\WinMetadata, which the
        // framework's AsTask() extensions cannot bind to, so awaiting a WinRT operation goes through this adapter
        private static Task<T> Await<T>(IAsyncOperation<T> operation)
        {
            TaskCompletionSource<T> done = new TaskCompletionSource<T>();
            operation.Completed = delegate (IAsyncOperation<T> info, AsyncStatus status)
            {
                if (status == AsyncStatus.Completed) done.TrySetResult(info.GetResults());
                else if (status == AsyncStatus.Canceled) done.TrySetCanceled();
                else done.TrySetException(info.ErrorCode);
            };
            return done.Task;
        }
    }
}
```

`TrayApp.cs`:
- `internal static class AutoStart { ... }` bloğunu ve `using Microsoft.Win32;` satırını sil.
- `Program.Main` içinde `new TrayContext(Array.IndexOf(args, AutoStart.Flag) >= 0)` → `new TrayContext(AutoStart.WasStartedAtSignIn(args))`.
- `TrayContext` yapıcısında `startupItem.Checked = AutoStart.IsEnabled;` satırını sil; `tray.Visible = true;` satırının hemen altına `ShowStartupState();` ekle.
- `ToggleStartup` metodunu şununla değiştir ve `ShowStartupState` ekle:

```csharp
        // Reading the Store package's startup task is asynchronous; the check mark appears once it is known
        private async void ShowStartupState()
        {
            try
            {
                startupItem.Checked = await AutoStart.GetStateAsync() == AutoStartState.Enabled;
            }
            catch (Exception)
            {
                startupItem.Checked = false;
            }
        }

        private async void ToggleStartup(object sender, EventArgs e)
        {
            bool enable = !startupItem.Checked;
            try
            {
                AutoStartState state = await AutoStart.SetEnabledAsync(enable);
                startupItem.Checked = state == AutoStartState.Enabled;
                if (!enable || state == AutoStartState.Enabled) return;
                if (state == AutoStartState.DisabledByUser)
                {
                    // Switched off in Settings or Task Manager: Windows lets only the user switch it back on there
                    Toast.Popup("Windows ile başlat Ayarlar'da kapalı", "Açılan sayfada Test Veri Üretici'yi aç", false, 5000);
                    AutoStart.OpenStartupSettings();
                }
                else
                {
                    Toast.Popup("Windows ile başlat açılamadı", "Bir ilke engelliyor olabilir", false, 4000);
                }
            }
            catch (Exception ex)
            {
                Toast.Popup("Ayar kaydedilemedi", ex.Message, false);
            }
        }
```

- [ ] **Step 4: Run** build — Expected: `Tum testler gecti.`, `Hazir: ...`.

- [ ] **Step 5: Paketsiz davranış değişmedi** — exe'yi elle ve `--autostart` ile aç, görünür pencere say (bildirim = 1, sessiz = 0):

```powershell
Add-Type -Name Win -Namespace W -MemberDefinition @'
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, System.IntPtr l);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr h, out uint pid);
public static int VisibleWindows(uint pid) { int n = 0; EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) n++; return true; }, System.IntPtr.Zero); return n; }
'@
$exe = "$PWD\TestVeriUretici.exe"
foreach ($a in @($null, '--autostart')) {
    Get-Process TestVeriUretici -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 700
    $p = if ($a) { Start-Process $exe -ArgumentList $a -PassThru } else { Start-Process $exe -PassThru }
    Start-Sleep -Milliseconds 1500; "$a -> $([W.Win]::VisibleWindows([uint32]$p.Id))"
}
```

Expected: ` -> 1` ve `--autostart -> 0`.

- [ ] **Step 6: Commit**

```bash
git add AutoStart.cs TrayApp.cs Tests.cs build.ps1
git commit -m "Windows ile başlat: Store paketinde StartupTask kullan" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: İkonun yerini okumak ve Store'a uygun açılış bildirimi

**Files:**
- Modify: `TrayPin.cs` (`IsPromoted`, ortak `OpenOurEntry`), `TrayApp.cs` (`TrayContext`), `Tests.cs`

**Interfaces:**
- Consumes: `AppPackage.IsPackaged`
- Produces: `bool TrayPin.IsPromoted()` — Windows ikonu şu an saatin yanında gösteriyorsa `true`; kayıt yoksa `false`.

- [ ] **Step 1: Failing test** — `Tests.cs`'e (Task 2 testlerinin altına):

```csharp
            // Tests.exe never shows a tray icon, so it has no entry and counts as hidden (Store version shows the hint)
            Expect("Tepsi kaydi olmayan exe gizli sayiliyor", !TrayPin.IsPromoted());
```

- [ ] **Step 2: Run** build — Expected: FAIL (`IsPromoted` tanımsız).

- [ ] **Step 3: Implement** — `TrayPin.cs`'de `TryPin` metodunu değiştir, `IsPromoted` ve `OpenOurEntry` ekle:

```csharp
        /// <summary>
        /// Whether Windows shows our icon next to the clock right now. It only reads, so it also works inside the
        /// Store package, where registry reads see the real values and only writes are redirected.
        /// </summary>
        public static bool IsPromoted()
        {
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(SettingsKey))
                using (RegistryKey icon = OpenOurEntry(root, false))
                {
                    object promoted = icon == null ? null : icon.GetValue("IsPromoted");
                    return promoted is int && (int)promoted == 1;
                }
            }
            catch (SecurityException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (IOException) { return false; }
        }

        // True when nothing is left to do: our entry was found, or this Windows has no such setting
        private static bool TryPin()
        {
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(SettingsKey))
                {
                    if (root == null) return true;
                    using (RegistryKey icon = OpenOurEntry(root, true))
                    {
                        if (icon == null) return false;
                        if (icon.GetValue("IsPromoted") == null) icon.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                        return true;
                    }
                }
            }
            // Pinning is a convenience; never let it take the app down
            catch (SecurityException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
            catch (IOException) { return true; }
        }

        // Our exe's entry among the per-icon settings, or null while Explorer has not registered the icon
        private static RegistryKey OpenOurEntry(RegistryKey root, bool writable)
        {
            if (root == null) return null;
            foreach (string name in root.GetSubKeyNames())
            {
                using (RegistryKey icon = root.OpenSubKey(name))
                {
                    if (icon == null || !IsThisExe(icon.GetValue("ExecutablePath") as string)) continue;
                }
                return root.OpenSubKey(name, writable);
            }
            return null;
        }
```

`TrayApp.cs`'de `TrayContext` yapıcısının sonunu değiştir ve yardımcıyı ekle:

```csharp
            // The Store package cannot move its icon (its registry writes stay private), so only the exe pins itself
            if (!AppPackage.IsPackaged) TrayPin.PinWhenRegistered();
            // Opened by hand: say where it went. Started with Windows: stay quiet.
            if (!startedAtSignIn)
                Toast.Popup("Test Veri Üretici çalışıyor", WhereIsTheIcon(), true, 4000);
        }

        private static string WhereIsTheIcon()
        {
            return AppPackage.IsPackaged && !TrayPin.IsPromoted()
                ? "İkon ^ altında, saatin yanına sürükle"
                : "Saatin yanındaki ID ikonuna sağ tıkla";
        }
```

- [ ] **Step 4: Run** build — Expected: `Tum testler gecti.`
- [ ] **Step 5: Paketsiz regresyon** — Task 2 Step 5 komutunu tekrar çalıştır; Expected aynı (` -> 1`, `--autostart -> 0`). `TryPin` yeniden yazıldığı için yeni bir klasörden ilk açılışta sabitlemeyi de sına:

```powershell
$t = "$env:TEMP\TestVeriUretici-pin"; New-Item -ItemType Directory -Force $t | Out-Null
Get-Process TestVeriUretici -ErrorAction SilentlyContinue | Stop-Process -Force
Copy-Item TestVeriUretici.exe $t; Start-Process "$t\TestVeriUretici.exe"; Start-Sleep -Seconds 5
$entry = Get-ChildItem "HKCU:\Control Panel\NotifyIconSettings" | Where-Object { (Get-ItemProperty $_.PSPath).ExecutablePath -eq "$t\TestVeriUretici.exe" }
"IsPromoted=" + (Get-ItemProperty $entry.PSPath).IsPromoted
Get-Process TestVeriUretici | Stop-Process -Force; Remove-Item $entry.PSPath -Recurse; Remove-Item $t -Recurse -Force
Start-Process "$PWD\TestVeriUretici.exe"
```

Expected: `IsPromoted=1`; kullanıcının asıl exe'sinin kaydında da `IsPromoted=1` kalır.
- [ ] **Step 6: Commit**

```bash
git add TrayPin.cs TrayApp.cs Tests.cs
git commit -m "Store sürümünde ikonun yerine göre açılış bildirimi" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Store görselleri ve paket manifesti

**Files:**
- Create: `store/AppxManifest.xml`
- Modify: `AppIcon.cs` (`SaveStoreAssets`), `TrayApp.cs` (`--make-store-assets`), `Tests.cs` (kök klasör argümanı + tutarlılık testleri), `build.ps1` (test listesine `AppIcon.cs`, testlere `$root`)

**Interfaces:**
- Consumes: `AutoStart.TaskId`, `AppIcon.Draw(int size)`
- Produces: `void AppIcon.SaveStoreAssets(string folder)`; exe argümanı `--make-store-assets <klasör>`; manifestte `$VERSION$` yer tutucusu.

- [ ] **Step 1: Failing tests** — `Tests.cs`: `Main()` → `Main(string[] args)`, başına `string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();`; `using System.Text.RegularExpressions;` ekle; Task 3 testinin altına:

```csharp
            // Every image and the StartupTask id in store\AppxManifest.xml must match what the code produces and uses
            string manifest = File.ReadAllText(Path.Combine(root, @"store\AppxManifest.xml"));
            string assets = Path.Combine(Path.GetTempPath(), "TestVeriUretici-store-assets");
            AppIcon.SaveStoreAssets(assets);
            foreach (Match image in Regex.Matches(manifest, @"Assets\\(\w+)\.png"))
                Expect("Manifest gorseli uretiliyor: " + image.Groups[1].Value,
                    Directory.GetFiles(assets, image.Groups[1].Value + ".*.png").Length > 0);
            Expect("Gorev cubugu ikonu (unplated) uretiliyor",
                File.Exists(Path.Combine(assets, "Square44x44Logo.targetsize-24_altform-unplated.png")));
            Expect("Manifest StartupTask kimligi kodla ayni", manifest.Contains("TaskId=\"" + AutoStart.TaskId + "\""));
            Expect("Manifest baslangic gorevi kapali geliyor", manifest.Contains("Enabled=\"false\""));
            Directory.Delete(assets, true);
```

`build.ps1`: `$testSources`'a `AppIcon.cs` ekle; test çalıştırma satırı `& "$obj\Tests.exe" $root`.

- [ ] **Step 2: Run** build — Expected: FAIL (`SaveStoreAssets` tanımsız).

- [ ] **Step 3: Implement** — `AppIcon.cs` başına `using System;`, sınıfa:

```csharp
        /// <summary>Package images named in store\AppxManifest.xml; the name qualifiers are what makepri indexes.</summary>
        public static void SaveStoreAssets(string folder)
        {
            Directory.CreateDirectory(folder);
            SaveScaled(folder, "Square44x44Logo", 44);
            SaveScaled(folder, "Square150x150Logo", 150);
            SaveScaled(folder, "StoreLogo", 50);
            foreach (int size in new[] { 16, 24, 32, 48, 256 })
            {
                SavePng(Path.Combine(folder, "Square44x44Logo.targetsize-" + size + ".png"), size);
                // Unplated: the taskbar and Start list show it without a colored plate behind it
                SavePng(Path.Combine(folder, "Square44x44Logo.targetsize-" + size + "_altform-unplated.png"), size);
            }
        }

        private static void SaveScaled(string folder, string name, int size)
        {
            foreach (int scale in new[] { 100, 125, 150, 200, 400 })
                SavePng(Path.Combine(folder, name + ".scale-" + scale + ".png"),
                    (int)Math.Round(size * scale / 100.0, MidpointRounding.AwayFromZero));
        }

        private static void SavePng(string path, int size)
        {
            using (Bitmap bmp = Draw(size))
                bmp.Save(path, ImageFormat.Png);
        }
```

`TrayApp.cs` `Program.Main`, `--make-icon` bloğunun altına:

```csharp
            if (args.Length == 2 && args[0] == "--make-store-assets")
            {
                AppIcon.SaveStoreAssets(args[1]);
                return;
            }
```

(`--make-icon` üstündeki yorumu "Build steps: build.ps1 embeds this .ico as the .exe icon and packs these images into the Store package" yap.)

`store/AppxManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- build.ps1 -Msix fills in $VERSION$ from AssemblyInfo.cs and packs this with the exe, its images and resources.pri -->
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap uap5 rescap">

  <!-- Temporary test identity until the values from Partner Center > Product identity are filled in -->
  <Identity Name="TestVeriUretici.Dev" Publisher="CN=TestVeriUretici-Dev" Version="$VERSION$" ProcessorArchitecture="x64" />

  <Properties>
    <DisplayName>Test Veri Üretici</DisplayName>
    <PublisherDisplayName>Alperen</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>

  <Resources>
    <Resource Language="tr-TR" />
  </Resources>

  <Applications>
    <Application Id="TestVeriUretici" Executable="TestVeriUretici.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="Test Veri Üretici"
        Description="TC Kimlik No, VKN ve IBAN test verisi üretip panoya kopyalar"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png" />
      <Extensions>
        <!-- Off until the user turns on "Windows ile başlat" from the menu; AutoStart.cs uses this TaskId -->
        <uap5:Extension Category="windows.startupTask" Executable="TestVeriUretici.exe" EntryPoint="Windows.FullTrustApplication">
          <uap5:StartupTask TaskId="TestVeriUretici" Enabled="false" DisplayName="Test Veri Üretici" />
        </uap5:Extension>
      </Extensions>
    </Application>
  </Applications>

  <Capabilities>
    <!-- Every desktop (Win32) app in a package needs it -->
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
```

- [ ] **Step 4: Run** build — Expected: `Tum testler gecti.`
- [ ] **Step 5: Commit**

```bash
git add AppIcon.cs TrayApp.cs Tests.cs build.ps1 store/AppxManifest.xml
git commit -m "Store paketi için manifest ve görseller" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `build.ps1 -Msix` ile paket üretimi

**Files:**
- Modify: `build.ps1`, `.gitignore`

**Interfaces:**
- Consumes: `--make-store-assets`, `store/AppxManifest.xml` (`$VERSION$`)
- Produces: `obj\msix\` (kayıt edilebilir paket klasörü) ve repo kökünde `TestVeriUretici.msix`

- [ ] **Step 1: Implement** — `build.ps1` en üstüne (yorumların altına) `param([switch] $Msix)` ekle, açıklama yorumuna "-Msix also builds TestVeriUretici.msix for the Microsoft Store (needs the Windows SDK)" satırını ekle; dosyanın sonuna:

```powershell
if (-not $Msix) { return }

# 4. Microsoft Store package: exe + manifest + images + resources.pri, packed with the Windows SDK tools
$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName | Select-Object -Last 1
if (-not $makeappx) { throw 'Store paketi icin Windows SDK gerekli (makeappx.exe bulunamadi)' }
$sdk = $makeappx.DirectoryName

function Invoke-Tool([string] $tool, [string[]] $arguments, [string] $what) {
    $out = & $tool @arguments
    if ($LASTEXITCODE -ne 0) { $out | Write-Host; throw "$what basarisiz" }
}

$version = [regex]::Match([IO.File]::ReadAllText((Join-Path $root 'AssemblyInfo.cs')),
    'AssemblyFileVersion\("(\d+\.\d+\.\d+\.\d+)"\)').Groups[1].Value
if ($version -notmatch '\.0$') { throw "Store surumunun son hanesi 0 olmali: $version" }

$layout = Join-Path $obj 'msix'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory $layout | Out-Null
Copy-Item $exe $layout
$manifest = [IO.File]::ReadAllText((Join-Path $root 'store\AppxManifest.xml')).Replace('$VERSION$', $version)
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

$assets = Start-Process -FilePath $exe -ArgumentList '--make-store-assets', "`"$layout\Assets`"" -Wait -PassThru
if ($assets.ExitCode -ne 0) { throw 'Store gorselleri uretilemedi' }

Invoke-Tool "$sdk\makepri.exe" @('createconfig', '/cf', "$obj\priconfig.xml", '/dq', 'tr-TR', '/pv', '10.0.0', '/o') 'makepri createconfig'
Invoke-Tool "$sdk\makepri.exe" @('new', '/pr', $layout, '/cf', "$obj\priconfig.xml", '/mn', "$layout\AppxManifest.xml", '/of', "$layout\resources.pri", '/o') 'makepri new'
$msix = Join-Path $root 'TestVeriUretici.msix'
Invoke-Tool "$sdk\makeappx.exe" @('pack', '/d', $layout, '/p', $msix, '/o') 'makeappx pack'
Write-Host "Store paketi: $msix (surum $version)"
```

`.gitignore`'a `*.msix` satırı ekle.

- [ ] **Step 2: Run** `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -Msix` — Expected: `Tum testler gecti.`, `Hazir: ...`, `Store paketi: ...TestVeriUretici.msix (surum 1.1.0.0)`.

- [ ] **Step 3: Paket içeriğini doğrula**

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [IO.Compression.ZipFile]::OpenRead("$PWD\TestVeriUretici.msix")
$z.Entries.FullName | Where-Object { $_ -notlike 'Assets/*' }; "gorsel: " + @($z.Entries | Where-Object FullName -like 'Assets/*').Count
$z.Dispose()
Select-String -Path obj\msix\AppxManifest.xml -Pattern 'Version="1\.1\.0\.0"','\$VERSION\$'
```

Expected: `AppxManifest.xml`, `TestVeriUretici.exe`, `resources.pri`, `AppxBlockMap.xml`, `[Content_Types].xml` listelenir; `gorsel: 25`; yalnızca `Version="1.1.0.0"` eşleşir (`$VERSION$` kalmamış).

- [ ] **Step 4: SDK'sız derleme bozulmadı** — `Select-String -Path build.ps1 -Pattern 'Windows Kits'` satır numarası, `if (-not $Msix) { return }` satırından büyük olmalı; `-Msix` olmadan `build.ps1` çalışınca `Store paketi` satırı basılmamalı.

- [ ] **Step 5: Commit**

```bash
git add build.ps1 .gitignore
git commit -m "build.ps1 -Msix: Microsoft Store paketi üret" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Paketi bu bilgisayarda test etmek (Geliştirici Modu açık)

**Files:**
- Create (repo dışında, geçici): `$env:TEMP\TestVeriUretici-harness\StartupHarness.cs`

**Interfaces:**
- Consumes: `obj\msix\`, `AutoStart`, `AppPackage`

- [ ] **Step 1: Kur ve aç** — çalışan exe'yi kapat (ortak mutex), paketi klasörden kaydet, Başlat menüsündeki gibi aç:

```powershell
Get-Process TestVeriUretici -ErrorAction SilentlyContinue | Stop-Process -Force
Add-AppxPackage -Register "$PWD\obj\msix\AppxManifest.xml"
$pfn = (Get-AppxPackage -Name TestVeriUretici.Dev).PackageFamilyName; $pfn
Start-Process "shell:AppsFolder\$pfn!TestVeriUretici"; Start-Sleep -Seconds 2
(Get-Process TestVeriUretici).Path
```

Expected: PFN `TestVeriUretici.Dev_...`; süreç yolu `...\obj\msix\TestVeriUretici.exe`.

- [ ] **Step 2: Store'da ikon sabitlenmiyor** — paket klasörünün `NotifyIconSettings` kaydında `IsPromoted` olmamalı (uygulama yazmadı):

```powershell
Get-ChildItem "HKCU:\Control Panel\NotifyIconSettings" | ForEach-Object { $v = Get-ItemProperty $_.PSPath; if ($v.ExecutablePath -like '*\obj\msix\TestVeriUretici.exe') { "IsPromoted=[$($v.IsPromoted)]" } }
```

Expected: `IsPromoted=[]`.

- [ ] **Step 3: StartupTask'ı paket kimliğiyle sına** — `StartupHarness.cs`:

```csharp
using System;
using System.IO;

namespace TestVeriUretici
{
    // Runs inside the package (Invoke-CommandInDesktopPackage) to exercise the StartupTask path without clicking the menu
    internal static class StartupHarness
    {
        [STAThread]
        private static void Main(string[] args)
        {
            using (StreamWriter log = new StreamWriter(args[0]))
            {
                try
                {
                    log.WriteLine("paketli=" + AppPackage.IsPackaged);
                    log.WriteLine("baslangic=" + AutoStart.GetStateAsync().Result);
                    log.WriteLine("ac=" + AutoStart.SetEnabledAsync(true).Result);
                    log.WriteLine("sonra=" + AutoStart.GetStateAsync().Result);
                    log.WriteLine("kapat=" + AutoStart.SetEnabledAsync(false).Result);
                }
                catch (Exception e)
                {
                    log.WriteLine("hata=" + e);
                }
            }
        }
    }
}
```

```powershell
$h = "$env:TEMP\TestVeriUretici-harness"; $fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"; $wm = "$env:WINDIR\System32\WinMetadata"
New-Item -ItemType Directory -Force $h | Out-Null
& "$fw\csc.exe" /nologo /codepage:65001 /target:exe "/out:$h\StartupHarness.exe" /r:System.Windows.Forms.dll "/r:$wm\Windows.ApplicationModel.winmd" "/r:$wm\Windows.Foundation.winmd" "/r:$fw\System.Runtime.dll" "$h\StartupHarness.cs" AutoStart.cs AppPackage.cs
Remove-Item "$h\sonuc.txt" -ErrorAction SilentlyContinue
Invoke-CommandInDesktopPackage -PackageFamilyName $pfn -AppId TestVeriUretici -Command "$h\StartupHarness.exe" -Args "`"$h\sonuc.txt`""
Start-Sleep -Seconds 3; Get-Content "$h\sonuc.txt"
```

Expected:
```
paketli=True
baslangic=Disabled
ac=Enabled
sonra=Enabled
kapat=Disabled
```

- [ ] **Step 4: Açılış bildirimi ipucu gösteriyor** — paketli sürümü yeniden aç ve 1 sn içinde sağ alt köşenin görüntüsünü al:

```powershell
Get-Process TestVeriUretici -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 700
Start-Process "shell:AppsFolder\$pfn!TestVeriUretici"; Start-Sleep -Milliseconds 1200
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type -Name Dpi -Namespace W -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[W.Dpi]::SetProcessDPIAware() | Out-Null
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$r = New-Object System.Drawing.Rectangle ($b.Right - 620), ($b.Bottom - 190), 620, 190
$bmp = New-Object System.Drawing.Bitmap $r.Width, $r.Height; $g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Location, [System.Drawing.Point]::Empty, $r.Size); $bmp.Save("$env:TEMP\TestVeriUretici-harness\paketli-bildirim.png"); $g.Dispose(); $bmp.Dispose()
```

Expected (görüntüyü aç): "Test Veri Üretici çalışıyor / İkon ^ altında, saatin yanına sürükle". Ekran kilitliyse görüntü kilit ekranını gösterir; o durumda Task 2 Step 5'teki pencere sayımıyla paketli sürecin 1 görünür penceresi olduğunu doğrula ve metni kullanıcı doğrulamasına ekle.

- [ ] **Step 5: Kullanıcının doğrulayacakları** (fare ve oturum gerektirir, raporda "kullanıcı doğrulayacak" diye listelenir):
  - Paketli sürüm açıkken ikona sağ tık → **TC üret** → Not Defteri'ne Ctrl+V: 11 haneli numara yapışır.
  - Menüden **Windows ile başlat** işaretlenince Ayarlar → Uygulamalar → Başlangıç'ta "Test Veri Üretici" açık görünür.
  - Oturumu kapatıp açınca uygulama başlar ve bildirim **çıkmaz** (`AppInstance.GetActivatedEventArgs` StartupTask ayrımı yerelde başka türlü tetiklenemez).

- [ ] **Step 6: İki sürüm aynı anda açılmıyor** — paketli sürüm açıkken GitHub exe'sini başlat:

```powershell
Start-Process "$PWD\TestVeriUretici.exe"; Start-Sleep -Seconds 6
@(Get-Process TestVeriUretici).Count; (Get-Process TestVeriUretici).Path
```

Expected: `1` ve yalnızca `...\obj\msix\TestVeriUretici.exe` (ikinci süreç "zaten çalışıyor" bildirimiyle kapanır).

- [ ] **Step 7: Temizle ve normal duruma dön**

```powershell
Get-Process TestVeriUretici -ErrorAction SilentlyContinue | Stop-Process -Force
Get-AppxPackage -Name TestVeriUretici.Dev | Remove-AppxPackage
Get-ChildItem "HKCU:\Control Panel\NotifyIconSettings" | Where-Object { (Get-ItemProperty $_.PSPath).ExecutablePath -like '*\obj\msix\*' } | Remove-Item -Recurse
Start-Process "$PWD\TestVeriUretici.exe"
@(Get-AppxPackage -Name TestVeriUretici.Dev).Count
```

Expected: `0` (paket kaldırıldı), kullanıcının GitHub exe'si yeniden çalışıyor.

---

### Task 7: Store sayfası malzemeleri

**Files:**
- Create: `PRIVACY.md`, `store/listing-tr.md`, `store/listing/logo-300.png`, `store/listing/logo-1080.png`, `store/listing/ekran-menu.png`, `store/listing/ekran-bildirim.png`
- Create (repo dışında, geçici): `$env:TEMP\TestVeriUretici-harness\Screenshots.cs`

- [ ] **Step 1: `PRIVACY.md`**

```markdown
# Gizlilik Politikası — Test Veri Üretici

Son güncelleme: 29 Eylül 2026

Test Veri Üretici **hiçbir kişisel veri toplamaz, saklamaz veya paylaşmaz** ve internete bağlanmaz.

- Ürettiği TC Kimlik No, VKN ve IBAN değerleri bilgisayarınızda rastgele üretilir ve yalnızca panoya kopyalanır.
- "Windows ile başlat" tercihi yalnızca bu bilgisayarda, Windows'un kendi başlangıç ayarlarında tutulur.
- Uygulama reklam, analiz veya hata raporu servisi kullanmaz.

Sorular için: https://github.com/alperennkls/TestVeriUretici/issues

## Privacy Policy (English)

Test Veri Üretici does not collect, store or share any personal data and makes no network connections. Generated test
values are created locally and only copied to the clipboard. The "start with Windows" choice is kept in Windows' own
startup settings on this device. No advertising, analytics or crash-reporting services are used.
```

- [ ] **Step 2: `store/listing-tr.md`** — Partner Center'a kopyalanacak metinler:

```markdown
# Microsoft Store sayfası — Partner Center'a girilecek metinler

## Açıklama
Test Veri Üretici, yazılım testlerinde ihtiyaç duyulan geçerli biçimli TC Kimlik No, Vergi Kimlik No ve IBAN değerlerini tek tıkla üretir.

Saatin yanındaki ID ikonuna sağ tıklayın ve ihtiyacınız olanı seçin:
• TC üret: kontrol haneleri doğru, 11 haneli TC Kimlik No
• VKN üret: Gelir İdaresi algoritmasına uygun, 10 haneli Vergi Kimlik No
• IBAN üret: mod 97 kontrollü, 26 karakterlik TR IBAN

Değer anında panoya kopyalanır; istediğiniz alana Ctrl+V ile yapıştırın. Kısa bir bildirim neyin kopyalandığını gösterir ve çalıştığınız pencereden odağı almaz.

SAP, ERP, e-fatura, form ve API testleri yapan geliştiriciler, test uzmanları ve danışmanlar için.

Yalnızca test amaçlıdır: değerler rastgele üretilir ve yalnızca biçim ile kontrol hanesi kurallarına uyar. Gerçek bir kişi, şirket veya hesapla ilişkisi yoktur; test ortamları dışında kullanmayın.

Açık kaynaklıdır (MIT lisansı): https://github.com/alperennkls/TestVeriUretici

## Kısa açıklama
Tek tıkla geçerli TC Kimlik No, VKN ve IBAN test verisi üretip panoya kopyalar.

## Ürün özellikleri
- Tek tıkla TC Kimlik No, VKN ve TR IBAN üretimi
- Kontrol haneleri resmi algoritmalara uygun
- Değer otomatik olarak panoya kopyalanır
- Odağı çalmayan kısa bildirim
- İsteğe bağlı: Windows açılışında sessizce başlar
- Veri toplamaz, internete bağlanmaz

## Arama terimleri (en fazla 7)
TC kimlik, VKN, IBAN, test verisi, vergi kimlik numarası, test data, SAP

## Bu sürümdeki yenilikler
İlk Microsoft Store sürümü.

## Diğer alanlar
- Telif hakkı: © 2026 Alperen
- Kategori: Geliştirici araçları
- Web sitesi: https://github.com/alperennkls/TestVeriUretici
- Destek: https://github.com/alperennkls/TestVeriUretici/issues
- Gizlilik politikası: https://github.com/alperennkls/TestVeriUretici/blob/main/PRIVACY.md

## Yaş derecelendirmesi (IARC anketi)
Uygulama türü olarak "Yardımcı program / verimlilik / diğer" seçilir; şiddet, cinsellik, kumar, kullanıcılar arası iletişim, konum paylaşımı ve dijital satın alma sorularının hepsine "Hayır". Beklenen sonuç: 3+ / Herkes.

## İnceleme ekibine notlar (Notes for certification)
Test Veri Üretici is a system tray utility with no main window. After launch, a short notification appears near the clock and an "ID" icon is added to the notification area (Windows may place it under the ^ overflow arrow). Right-click the icon and choose "TC üret", "VKN üret" or "IBAN üret": a randomly generated, checksum-valid Turkish national ID number, tax ID number or TR IBAN is copied to the clipboard for use as software test data. "Windows ile başlat" toggles the app's startup task; "Çıkış" exits.
The values are random and only satisfy the public checksum rules (the same algorithms are used by common open-source test-data libraries); they belong to no real person, company or account, and the app states that they are for testing only. The app collects no data and makes no network connections.

## runFullTrust gerekçesi (Restricted capability justification)
The app is a Windows Forms (.NET Framework 4.8) desktop application packaged as MSIX; runFullTrust is required to run it as a desktop app. It shows a notification-area icon and writes generated test values to the clipboard. It does not modify system settings.
```

- [ ] **Step 3: Logolar ve ekran görüntüleri** — `Screenshots.cs` (gerçek menü ve bildirim, temiz arka plan ve sade bir görev çubuğu köşesi üzerinde; kullanıcının masaüstü görünmez):

```csharp
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TestVeriUretici
{
    // Store listing images: logos, plus the real tray menu and notice over a clean backdrop instead of the user's desktop
    internal static class Screenshots
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] private static extern bool EndMenu();

        [STAThread]
        private static void Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            string outDir = args[0];
            using (Bitmap logo = AppIcon.Draw(300)) logo.Save(outDir + "\\logo-300.png", ImageFormat.Png);
            using (Bitmap logo = AppIcon.Draw(1080)) logo.Save(outDir + "\\logo-1080.png", ImageFormat.Png);

            Rectangle screen = Screen.PrimaryScreen.Bounds;
            using (Backdrop backdrop = new Backdrop(screen, Screen.PrimaryScreen.WorkingArea))
            {
                backdrop.Show();
                Pump(500);
                ContextMenu menu = new ContextMenu(new[]
                {
                    new MenuItem("TC üret"), new MenuItem("VKN üret"), new MenuItem("IBAN üret"), new MenuItem("-"),
                    new MenuItem("Windows ile başlat"), new MenuItem("Çıkış")
                });
                Timer shot = new Timer { Interval = 600 };
                shot.Tick += delegate
                {
                    shot.Stop();
                    Capture(screen, outDir + "\\ekran-menu.png");
                    EndMenu();
                };
                shot.Start();
                menu.Show(backdrop, backdrop.IconPoint); // modal until EndMenu
                Toast.Popup("TC kopyalandı", "10000000146", true, 3000);
                Pump(700);
                Capture(screen, outDir + "\\ekran-bildirim.png");
            }
        }

        private static void Pump(int ms)
        {
            DateTime until = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); }
        }

        private static void Capture(Rectangle screen, string path)
        {
            using (Bitmap bmp = new Bitmap(screen.Width, screen.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(screen.Location, Point.Empty, screen.Size);
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }

    // Full-screen clean background with a minimal dark taskbar corner: ^, the ID icon and a clock
    internal sealed class Backdrop : Form
    {
        private readonly Rectangle bar;
        private readonly Rectangle icon;

        public Backdrop(Rectangle screen, Rectangle workingArea)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screen;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            bar = new Rectangle(0, workingArea.Bottom - screen.Top, screen.Width, screen.Bottom - workingArea.Bottom);
            int size = SystemInformation.SmallIconSize.Width;
            icon = new Rectangle(bar.Right - bar.Height * 4, bar.Top + (bar.Height - size) / 2, size, size);
        }

        public Point IconPoint
        {
            get { return new Point(icon.Left + icon.Width / 2, bar.Top); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (LinearGradientBrush sky = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(28, 46, 84), Color.FromArgb(92, 120, 170), LinearGradientMode.ForwardDiagonal))
                g.FillRectangle(sky, ClientRectangle);
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(32, 32, 32)))
                g.FillRectangle(dark, bar);
            using (Bitmap id = AppIcon.Draw(icon.Width)) g.DrawImage(id, icon);
            TextRenderer.DrawText(g, "^", Font, new Rectangle(icon.Left - bar.Height, bar.Top, bar.Height, bar.Height), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "14:30\n29.09.2026", Font, new Rectangle(bar.Right - bar.Height * 3, bar.Top, bar.Height * 3, bar.Height),
                Color.White, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }
}
```

**Kullanıcıya önce haber ver** ("ekranın 3 saniye boyunca bir arka planla kaplanacak"), onay gelince çalıştır:

```powershell
$h = "$env:TEMP\TestVeriUretici-harness"; $fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force store\listing | Out-Null
& "$fw\csc.exe" /nologo /codepage:65001 /target:winexe "/out:$h\Screenshots.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "$h\Screenshots.cs" Toast.cs AppIcon.cs
Start-Process "$h\Screenshots.exe" -ArgumentList "`"$PWD\store\listing`"" -Wait
Get-ChildItem store\listing | Select-Object Name, Length
```

Expected: 4 PNG. Görselleri aç ve kontrol et: ekran görüntülerinde menü ve bildirim görünüyor, kullanıcının masaüstünden hiçbir şey yok, boyut ≥ 1366×768.

- [ ] **Step 4: Commit**

```bash
git add PRIVACY.md store/listing-tr.md store/listing
git commit -m "Store sayfası metinleri, gizlilik politikası ve görseller" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Gerçek kimlikle v1.2.0 Store paketi

**Kullanıcıdan gelecek:** Partner Center → uygulama → Product management → **Product identity** sayfasındaki üç değer.

**Files:**
- Modify: `store/AppxManifest.xml` (kimlik), `AssemblyInfo.cs` (1.2.0.0)

- [ ] **Step 1: Kimliği yaz** — `store/AppxManifest.xml`:
  - `Identity Name` ← Product identity sayfasındaki `Package/Identity/Name`
  - `Identity Publisher` ← `Package/Identity/Publisher` (`CN=` ile başlayan değer, aynen)
  - `PublisherDisplayName` ← `Package/Properties/PublisherDisplayName`
  - "Temporary test identity" yorumunu "Values from Partner Center > Product identity; they must match exactly" yap.
- [ ] **Step 2: Sürüm** — `AssemblyInfo.cs` içinde iki satırı `1.2.0.0` yap.
- [ ] **Step 3: Build** `build.ps1 -Msix` — Expected: `Store paketi: ... (surum 1.2.0.0)`.
- [ ] **Step 4: Paketteki kimliği doğrula** — `obj\msix\AppxManifest.xml` içindeki `Identity` satırını yazdır; üç değer Partner Center'dakiyle karakter karakter aynı.
- [ ] **Step 5: Duman testi** — Task 6 Step 1'i gerçek kimlikle çalıştır (`Get-AppxPackage -Name <Identity Name>`), bildirim ve ikon görülsün, sonra Task 6 Step 7 ile **mutlaka kaldır**: aynı kimlikle kayıtlı bir geliştirici paketi, Store'dan kurulumla çakışır.
- [ ] **Step 6: Commit**

```bash
git add store/AppxManifest.xml AssemblyInfo.cs
git commit -m "Store kimliği ve sürüm 1.2.0" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: README, GitHub sürümü ve gönderim rehberi

**Files:**
- Modify: `README.md`

- [ ] **Step 1: README** —
  - İndir bağlantısının yanına Store bağlantısı: `https://apps.microsoft.com/detail/<Store ID>` (Store ID Product identity sayfasında; bağlantı yayından sonra çalışır) ve "Store'dan kurulumda Windows uyarısı çıkmaz" notu.
  - Dosya tablosuna: `AppPackage.cs` (Store paketinde çalıştığını algılama), `AutoStart.cs` (Windows ile başlat: Run kaydı / StartupTask), `store/` (Store paketi manifesti ve sayfa metinleri), `PRIVACY.md`.
  - Kaynaktan derleme: "`build.ps1 -Msix` Store paketini de üretir (Windows SDK gerekir)".
- [ ] **Step 2: Build** `build.ps1` — Expected: `Tum testler gecti.`
- [ ] **Step 3: Commit ve push**

```bash
git add README.md
git commit -m "README: Microsoft Store sürümü" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

- [ ] **Step 4: GitHub v1.2.0** — v1.1.0 ile aynı yöntem (taslak release → exe yükle → yayınla; SHA-256 notlarda). Notlar: "Microsoft Store sürümüne hazırlık; GitHub exe'sinin davranışı değişmedi." Anonim indirip SHA-256 karşılaştır.
- [ ] **Step 5: Kullanıcıya Partner Center gönderim rehberi** — sırasıyla: Fiyatlandırma ve kullanılabilirlik (Ücretsiz, tüm pazarlar) → Özellikler (kategori, gizlilik URL'si, destek) → Yaş derecelendirmesi (`store/listing-tr.md`) → Paketler (`TestVeriUretici.msix` yükle) → Store listesi, Türkçe (metinler + `store/listing` görselleri) → Gönderim seçenekleri (inceleme notu + runFullTrust gerekçesi) → Gönder.

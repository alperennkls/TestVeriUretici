# Builds TestVeriUretici.exe with the C# compiler that ships with Windows (.NET Framework 4.x),
# so nothing has to be installed. The generator tests run first; a failure stops the build.
# -Msix also builds TestVeriUretici.msix for the Microsoft Store (needs the Windows SDK).
param([switch] $Msix)
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$obj = Join-Path $root 'obj'
$exe = Join-Path $root 'TestVeriUretici.exe'
$ico = Join-Path $obj 'TestVeriUretici.ico'
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
# WinRT (StartupTask for the Store package) through the metadata every Windows 10/11 has, so no SDK is needed
$winmd = Join-Path $env:WINDIR 'System32\WinMetadata'
$common = '/nologo', '/codepage:65001', '/optimize+', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
    "/r:$winmd\Windows.ApplicationModel.winmd", "/r:$winmd\Windows.Foundation.winmd", "/r:$fw\System.Runtime.dll"
$appSources = 'Generators.cs', 'TrayApp.cs', 'TrayPin.cs', 'AutoStart.cs', 'AppPackage.cs', 'Toast.cs', 'AppIcon.cs', 'AssemblyInfo.cs' | ForEach-Object { Join-Path $root $_ }
$testSources = 'Generators.cs', 'TrayPin.cs', 'AutoStart.cs', 'AppPackage.cs', 'AppIcon.cs', 'Tests.cs' | ForEach-Object { Join-Path $root $_ }

function Invoke-Csc([string[]] $arguments) {
    & $csc @common @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Derleme hatasi' }
}

New-Item -ItemType Directory -Force $obj | Out-Null

# 1. Tests
Invoke-Csc (@('/target:exe', "/out:$obj\Tests.exe") + $testSources)
& "$obj\Tests.exe" $root
if ($LASTEXITCODE -ne 0) { throw 'Testler basarisiz, exe uretilmedi' }

# 2. The app draws its own icon: build once without it and export the .ico
Invoke-Csc (@('/target:winexe', "/out:$obj\IconMaker.exe") + $appSources)
$maker = Start-Process -FilePath "$obj\IconMaker.exe" -ArgumentList '--make-icon', "`"$ico`"" -Wait -PassThru
if ($maker.ExitCode -ne 0) { throw 'Ikon uretilemedi' }

# 3. Final exe; a running copy would keep the file locked
$running = Get-Process -Name 'TestVeriUretici' -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 5
}
Invoke-Csc (@('/target:winexe', "/win32icon:$ico", "/out:$exe") + $appSources)
Write-Host "Hazir: $exe"

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
# One package, not a bundle: keep every image variant in resources.pri instead of per-scale split files
$priconfig = [xml][IO.File]::ReadAllText("$obj\priconfig.xml")
$packaging = $priconfig.resources.packaging
if ($packaging) { [void]$packaging.ParentNode.RemoveChild($packaging) }
$priconfig.Save("$obj\priconfig.xml")
Invoke-Tool "$sdk\makepri.exe" @('new', '/pr', $layout, '/cf', "$obj\priconfig.xml", '/mn', "$layout\AppxManifest.xml", '/of', "$layout\resources.pri", '/o') 'makepri new'
$package = Join-Path $root 'TestVeriUretici.msix'  # not $msix: PowerShell names ignore case, that is the -Msix switch
Invoke-Tool "$sdk\makeappx.exe" @('pack', '/d', $layout, '/p', $package, '/o') 'makeappx pack'
Write-Host "Store paketi: $package (surum $version)"

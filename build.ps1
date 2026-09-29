# Builds TestVeriUretici.exe with the C# compiler that ships with Windows (.NET Framework 4.x),
# so nothing has to be installed. The generator tests run first; a failure stops the build.
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$obj = Join-Path $root 'obj'
$exe = Join-Path $root 'TestVeriUretici.exe'
$ico = Join-Path $obj 'TestVeriUretici.ico'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$common = '/nologo', '/codepage:65001', '/optimize+', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll'
$appSources = 'Generators.cs', 'TrayApp.cs', 'TrayPin.cs', 'AppPackage.cs', 'Toast.cs', 'AppIcon.cs', 'AssemblyInfo.cs' | ForEach-Object { Join-Path $root $_ }
$testSources = 'Generators.cs', 'TrayPin.cs', 'AppPackage.cs', 'Tests.cs' | ForEach-Object { Join-Path $root $_ }

function Invoke-Csc([string[]] $arguments) {
    & $csc @common @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Derleme hatasi' }
}

New-Item -ItemType Directory -Force $obj | Out-Null

# 1. Tests
Invoke-Csc (@('/target:exe', "/out:$obj\Tests.exe") + $testSources)
& "$obj\Tests.exe"
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

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc  = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$game = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
$bin  = Join-Path $game "bin\Win64_Shipping_Client"
$fw   = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$out  = Join-Path $root "build\out"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$refs = @(
  "$fw\mscorlib.dll", "$fw\System.dll", "$fw\System.Core.dll", "$fw\System.Xml.dll",
  "$bin\mono\lib\mono\4.5\Facades\netstandard.dll",
  "$bin\TaleWorlds.CampaignSystem.dll", "$bin\TaleWorlds.Core.dll",
  "$bin\TaleWorlds.Library.dll", "$bin\TaleWorlds.ObjectSystem.dll",
  "$bin\TaleWorlds.Localization.dll", "$bin\TaleWorlds.MountAndBlade.dll",
  "$bin\TaleWorlds.ModuleManager.dll"
)
foreach ($r in $refs) {
  if (-not (Test-Path $r)) { Write-Host "MISSING REFERENCE: $r"; exit 1 }
}

# MCM, referenced to compile against and never required to run. Everything that
# names an MCM type lives behind McmBridge, which catches the load failure, so a
# player without MCM keeps settings.xml and loses only the options screen. Built
# against whatever copy this machine has; the mod does not ship it.
$mcm = Get-ChildItem -Path (Join-Path $game "..\..\workshop\content\261550") -Recurse `
         -Filter "MCMv5.dll" -File -ErrorAction SilentlyContinue |
       Select-Object -First 1
if (-not $mcm) {
  $mcm = Get-ChildItem -Path (Join-Path $game "Modules") -Recurse `
           -Filter "MCMv5.dll" -File -ErrorAction SilentlyContinue |
         Select-Object -First 1
}
if (-not $mcm) { Write-Host "MISSING REFERENCE: MCMv5.dll (install Mod Configuration Menu v5)"; exit 1 }
$refs += $mcm.FullName

$sources = Get-ChildItem (Join-Path $root "src") -Recurse -Filter *.cs

$rsp = Join-Path $out "mod.rsp"
$lines = @("/nologo", "/target:library", "/platform:x64", "/optimize+", "/out:`"$out\HeroesEvolve.dll`"")
foreach ($r in $refs)    { $lines += "/r:`"$r`"" }
foreach ($s in $sources) { $lines += "`"$($s.FullName)`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

& $csc /noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED"; exit 1 }

$dest = Join-Path $game "Modules\HeroesEvolve"
New-Item -ItemType Directory -Force -Path (Join-Path $dest "bin\Win64_Shipping_Client") | Out-Null
Copy-Item (Join-Path $root "SubModule.xml") $dest -Force

# settings.xml is parsed before it is allowed to ship. This has gone wrong twice
# the same way: an em dash typed as two hyphens inside a comment, which XML
# forbids, and which nothing complains about at the time. Settings.Load catches
# the parse failure by design -- a broken config must never take a campaign down
# -- so the file silently falls back to defaults in its entirety, and the only
# symptom is a mod that quietly ignores every setting in it. Cheap to check,
# invisible when it breaks, so it is checked.
$sourceSettings = Join-Path $root "settings.xml"
try {
  $probe = New-Object System.Xml.XmlDocument
  $probe.Load($sourceSettings)
} catch {
  Write-Host "settings.xml is not valid XML and would load as defaults:" -ForegroundColor Red
  Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
  Write-Host "  (a comment containing -- is the usual cause; use an em dash)" -ForegroundColor Yellow
  exit 1
}

# settings.xml ships alongside, but never over the top of one the player has
# already edited -- a deploy must not silently reset their configuration.
$settings = Join-Path $dest "settings.xml"
if (-not (Test-Path $settings)) { Copy-Item $sourceSettings $settings -Force }
Copy-Item "$out\HeroesEvolve.dll" (Join-Path $dest "bin\Win64_Shipping_Client") -Force

# Translations. Replaced wholesale rather than merged, so a renamed or deleted
# language file cannot linger in the deployed module and go on being loaded.
$moduleData = Join-Path $root "ModuleData"
if (Test-Path $moduleData) {
    $destData = Join-Path $dest "ModuleData"
    if (Test-Path $destData) { Remove-Item $destData -Recurse -Force }
    Copy-Item $moduleData $destData -Recurse -Force
}

Write-Host "DEPLOYED to $dest"

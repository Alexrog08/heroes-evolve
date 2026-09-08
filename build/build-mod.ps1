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
  "$bin\TaleWorlds.Localization.dll", "$bin\TaleWorlds.MountAndBlade.dll"
)
foreach ($r in $refs) {
  if (-not (Test-Path $r)) { Write-Host "MISSING REFERENCE: $r"; exit 1 }
}

$sources = Get-ChildItem (Join-Path $root "src") -Recurse -Filter *.cs

$rsp = Join-Path $out "mod.rsp"
$lines = @("/nologo", "/target:library", "/platform:x64", "/optimize+", "/out:`"$out\HeroLoadoutFixer.dll`"")
foreach ($r in $refs)    { $lines += "/r:`"$r`"" }
foreach ($s in $sources) { $lines += "`"$($s.FullName)`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

& $csc /noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED"; exit 1 }

$dest = Join-Path $game "Modules\HeroLoadoutFixer"
New-Item -ItemType Directory -Force -Path (Join-Path $dest "bin\Win64_Shipping_Client") | Out-Null
Copy-Item (Join-Path $root "SubModule.xml") $dest -Force

# settings.xml ships alongside, but never over the top of one the player has
# already edited -- a deploy must not silently reset their configuration.
$settings = Join-Path $dest "settings.xml"
if (-not (Test-Path $settings)) { Copy-Item (Join-Path $root "settings.xml") $settings -Force }
Copy-Item "$out\HeroLoadoutFixer.dll" (Join-Path $dest "bin\Win64_Shipping_Client") -Force

# Translations. Replaced wholesale rather than merged, so a renamed or deleted
# language file cannot linger in the deployed module and go on being loaded.
$moduleData = Join-Path $root "ModuleData"
if (Test-Path $moduleData) {
    $destData = Join-Path $dest "ModuleData"
    if (Test-Path $destData) { Remove-Item $destData -Recurse -Force }
    Copy-Item $moduleData $destData -Recurse -Force
}

Write-Host "DEPLOYED to $dest"

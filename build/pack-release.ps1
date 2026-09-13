# Builds the release archive -- the file that goes up on Nexus.
#
# Steam does not need this: the Workshop publisher uploads the deployed module
# folder itself. Nexus takes a zip, and the zip is the only artifact of this
# project assembled by hand. It drifted within a day of being made: the archive
# carried a build from the ninth while the module had been rebuilt on the tenth,
# and nothing said so, because a zip has no way to complain about its age.
#
# So it is built here instead, from the repository, with the one check that
# would have caught that drift.
#
# Assembled from the REPOSITORY and not from the deployed module, deliberately.
# build-mod.ps1 refuses to copy settings.xml over one that already exists --
# a deploy must never reset a player's configuration -- which means the
# deployed settings.xml is whatever THIS machine's player last saved. Packing
# that would ship one person's tuning to everyone who downloads the mod.

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dll  = Join-Path $root "build\out\HeroesEvolve.dll"
$out  = Join-Path $root ".tmp"

if (-not (Test-Path $dll)) { throw "no build to package; run build/build-mod.ps1 first" }

# The version comes from SubModule.xml, which is what the launcher shows and
# what the Workshop page reads. Naming the zip from anything else lets the two
# disagree, and the download is then the only place the version is wrong.
$submodule = Join-Path $root "SubModule.xml"
$xml = New-Object System.Xml.XmlDocument
$xml.Load($submodule)
$version = $xml.SelectSingleNode("//Version").GetAttribute("value")
if ([string]::IsNullOrWhiteSpace($version)) { throw "no <Version value=...> in SubModule.xml" }

# The check that this script exists for. A source file newer than the binary
# means the archive would ship code the author has already changed -- silently,
# since the zip looks the same either way.
$stale = Get-ChildItem (Join-Path $root "src") -Recurse -Filter *.cs |
         Where-Object { $_.LastWriteTime -gt (Get-Item $dll).LastWriteTime }
if ($stale) {
    Write-Host "the build is older than the source; packaging it would ship stale code:" -ForegroundColor Red
    $stale | ForEach-Object { Write-Host "  $($_.FullName)" -ForegroundColor Red }
    Write-Host "  run build/build-mod.ps1 first" -ForegroundColor Yellow
    exit 1
}

# Same reason build-mod.ps1 parses it before deploying: an invalid settings.xml
# is caught by Settings.Load and falls back to defaults in its entirety, so a
# player gets a mod that ignores every setting in the file and no error anywhere.
try {
    $probe = New-Object System.Xml.XmlDocument
    $probe.Load((Join-Path $root "settings.xml"))
} catch {
    Write-Host "settings.xml is not valid XML and would load as defaults:" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  (a comment containing -- is the usual cause; use an em dash)" -ForegroundColor Yellow
    exit 1
}

# HeroesEvolve/ at the root, matching what the first release shipped: the player
# drops that folder into Modules\. Changing the shape now would break anyone
# updating over the top of an install.
$stage  = Join-Path $out "pack"
$module = Join-Path $stage "HeroesEvolve"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $module "bin\Win64_Shipping_Client") | Out-Null

Copy-Item $submodule                      $module -Force
Copy-Item (Join-Path $root "settings.xml") $module -Force
Copy-Item (Join-Path $root "LICENSE")      $module -Force
Copy-Item (Join-Path $root "ModuleData")   (Join-Path $module "ModuleData") -Recurse -Force
Copy-Item $dll (Join-Path $module "bin\Win64_Shipping_Client") -Force

$zip = Join-Path $out "HeroesEvolve-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $module -DestinationPath $zip -CompressionLevel Optimal

$size = [math]::Round((Get-Item $zip).Length / 1KB)
Write-Host "PACKED $zip  ($size KB)" -ForegroundColor Green
Get-ChildItem $module -Recurse -File |
    ForEach-Object { Write-Host "  $($_.FullName.Substring($stage.Length + 1))" }

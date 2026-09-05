$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc  = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$out  = Join-Path $root "build\out"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$sources = @()
# -Recurse: build-mod.ps1 already recurses src/. Without it here, a future
# subdirectory under src/Core (or tests/) would silently drop out of the test
# build and CoreTests.exe would still print a green "N passed, 0 failed".
$sources += Get-ChildItem (Join-Path $root "src\Core") -Recurse -Filter *.cs -ErrorAction SilentlyContinue
$sources += Get-ChildItem (Join-Path $root "tests")    -Recurse -Filter *.cs -ErrorAction SilentlyContinue

$rsp = Join-Path $out "tests.rsp"
$lines = @("/nologo", "/target:exe", "/platform:x64", "/out:`"$out\CoreTests.exe`"")
$lines += Get-Content (Join-Path $root "build\refs.rsp")
foreach ($s in $sources) { $lines += "`"$($s.FullName)`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

& $csc /noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED"; exit 1 }

& "$out\CoreTests.exe"
exit $LASTEXITCODE

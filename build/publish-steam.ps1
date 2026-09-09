# Builds the Steam Workshop configuration file the game's own publisher reads.
#
# TaleWorlds.MountAndBlade.SteamWorkshop.exe takes exactly one argument, a path
# to an XML file, and runs the tasks inside it in order. Everything below was
# read out of that tool rather than guessed:
#
#   Program.LoadTasks      root's children are CreateItem, UpdateItem, GetItem
#   UpdateItemTask.LoadFrom  ModuleFolder, ItemDescription, Image, Tags,
#                            Visibility, ChangeNotes -- each a child element
#                            carrying a Value attribute; Tags is a container
#                            whose children each carry one
#   UpdateItemTask.DoJob   the title is NOT in the file. It comes from
#                          ModuleInfo.Name, which is SubModule.xml's
#                          <Name value="..."/>. ModuleFolder is a full path.
#   GetItemTask.LoadFrom   ItemId, for updating an item that already exists
#
# The tool also refuses to start unless Steam is running, you are logged in,
# and Steam Cloud is enabled both for your account and for Bannerlord.
#
# This script only WRITES the file and prints the command. It does not publish:
# publishing puts the mod on the internet under your Steam account, and that is
# yours to trigger.

param(
    # Workshop item id. Omit on the very first publish -- the config then asks
    # the tool to create a new item. Pass it for every publish after that, or a
    # second, duplicate Workshop page is what you get.
    [string]$ItemId = "",

    # Shown in the Workshop's change history.
    [string]$ChangeNotes = "",

    # private, public or friendsonly. Anything else is ignored by the tool and
    # leaves its default, so this script refuses the typo instead.
    [ValidateSet("private", "public", "friendsonly")]
    [string]$Visibility = "private",

    # A .jpg or .png, 512x512 or larger. Optional: the tool skips the preview
    # when the element is absent, and Steam lets you add one on the web page.
    [string]$Image = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$game = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
$module = Join-Path $game "Modules\HeroesEvolve"
$publisher = Join-Path $game "bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe"
$descriptionFile = Join-Path $root "docs\store-description.bbcode"

foreach ($p in @($module, $publisher, $descriptionFile)) {
    if (-not (Test-Path $p)) { throw "not found: $p" }
}

# The description lives in one place and is read from it, so the Workshop page
# and the Nexus page cannot drift apart.
$description = Get-Content $descriptionFile -Raw

# XML normalises a literal newline inside an attribute value into a space --
# it is in the spec, and it would flatten the whole description into one
# paragraph. A character reference survives normalisation, so the line breaks
# have to go in as &#10; and the escaping has to happen in this order.
function Escape-Attr([string]$s) {
    $s = $s -replace '&', '&amp;'
    $s = $s -replace '<', '&lt;'
    $s = $s -replace '>', '&gt;'
    $s = $s -replace '"', '&quot;'
    $s = $s -replace "`r`n", "`n"
    $s = $s -replace "`n", '&#10;'
    return $s
}

$tasks = New-Object System.Collections.Generic.List[string]
if ([string]::IsNullOrWhiteSpace($ItemId)) {
    $tasks.Add('  <CreateItem />')
    Write-Host "No ItemId given: this config CREATES a new Workshop item." -ForegroundColor Yellow
} else {
    $tasks.Add('  <GetItem ItemId="' + (Escape-Attr $ItemId) + '" />')
    Write-Host "Updating existing Workshop item $ItemId."
}

$update = New-Object System.Collections.Generic.List[string]
$update.Add('  <UpdateItem>')
$update.Add('    <ModuleFolder Value="' + (Escape-Attr $module) + '" />')
$update.Add('    <ItemDescription Value="' + (Escape-Attr $description) + '" />')
$update.Add('    <Visibility Value="' + $Visibility + '" />')
$update.Add('    <Tags>')
$update.Add('      <Tag Value="Singleplayer" />')
$update.Add('    </Tags>')
if (-not [string]::IsNullOrWhiteSpace($Image)) {
    if (-not (Test-Path $Image)) { throw "preview image not found: $Image" }
    $update.Add('    <Image Value="' + (Escape-Attr (Resolve-Path $Image)) + '" />')
}
if (-not [string]::IsNullOrWhiteSpace($ChangeNotes)) {
    $update.Add('    <ChangeNotes Value="' + (Escape-Attr $ChangeNotes) + '" />')
}
$update.Add('  </UpdateItem>')

# Nexus renders a narrower BBCode dialect than Steam does -- no [h1]/[h2] --
# and would print those tags as literal text in the middle of the page. Rather
# than keep a second description that drifts from the first, the Nexus copy is
# rendered from the same source every time this runs, using only tags both
# sites agree on. Edit docs/store-description.bbcode; never edit the output.
$nexus = $description
$nexus = $nexus -replace '\[h1\](.*?)\[/h1\]', '[size=5][b]$1[/b][/size]'
$nexus = $nexus -replace '\[h2\](.*?)\[/h2\]', '[size=4][b]$1[/b][/size]'
$nexusOut = Join-Path $root ".tmp\store-description-nexus.bbcode"
New-Item -ItemType Directory -Force (Split-Path $nexusOut) | Out-Null
$nexus | Set-Content -Path $nexusOut -Encoding UTF8 -NoNewline
if ($nexus -match '\[h[0-9]\]') { throw "a header tag survived the Nexus rendering" }

$out = Join-Path $root ".tmp\workshop.xml"
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

# NO XML declaration. Program.LoadTasks reads document.FirstChild.ChildNodes,
# and with a <?xml ... ?> prolog present FirstChild is the XmlDeclaration, whose
# ChildNodes is empty -- so the tool loads zero tasks, does nothing, and prints
# "Starting..." then "Finished..." with no error of any kind. Cost an evening.
# Without the prolog FirstChild is <Tasks> and the tasks are its children.
$xml = @('<Tasks>') + $tasks + $update + @('</Tasks>')
$xml -join "`r`n" | Set-Content -Path $out -Encoding UTF8

# Checked the way the tool reads it, not the way XML says to read it. Loading
# into DocumentElement would have called the broken file perfectly good.
$check = New-Object System.Xml.XmlDocument
$check.Load($out)
$seen = $check.FirstChild.ChildNodes.Count
if ($check.FirstChild.NodeType -ne 'Element' -or $seen -lt 2) {
    throw "the publisher would read $seen tasks from this file; it needs the root element first and at least a create/get plus an update"
}
Write-Host "wrote $out ($seen tasks, as the publisher will read them)" -ForegroundColor Green

$version = ([xml](Get-Content (Join-Path $module "SubModule.xml"))).Module.Version.value
$name = ([xml](Get-Content (Join-Path $module "SubModule.xml"))).Module.Name.value
Write-Host ""
Write-Host "  title      $name   (from SubModule.xml, not from this file)"
Write-Host "  version    $version"
Write-Host "  module     $module"
Write-Host "  visibility $Visibility"
if ([string]::IsNullOrWhiteSpace($Image)) {
    Write-Host "  preview    none -- add one on the Workshop page afterwards" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Nexus copy (paste into the Description box):" -ForegroundColor Cyan
Write-Host "  $nexusOut"
Write-Host ""
Write-Host "Steam must be running and logged in. Then publish with:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  & `"$publisher`" `"$out`""
Write-Host ""

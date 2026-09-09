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

    # A .jpg or .png, 512x512 or larger. Defaults to the cover kept beside the
    # store copy, and a relative path is resolved against the repository rather
    # than against wherever you happened to be standing when you ran this.
    [string]$Image = "docs/cover.jpg",

    # Workshop tags. Steam defines the valid set for this app and silently
    # discards anything else, so they are checked here against the list the
    # Bannerlord workshop actually offers -- an invented tag would cost nothing
    # at publish time and simply never appear.
    [string[]]$Tags = @("Singleplayer", "Native", "Utility", "v1.4.8"),

    # Actually run the publisher after writing the config.
    #
    # Off by default: publishing puts the mod on the internet under your Steam
    # account, and that stays a decision you make rather than a side effect of
    # regenerating a file. Passing it is the decision.
    #
    # It exists because the alternative was a printed command line containing an
    # ampersand inside a path, which PowerShell mis-parses at the slightest
    # provocation and did so twice.
    [switch]$Publish
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

# Steam caps a Workshop description at 8000 characters and does not say so
# politely: the whole upload runs, the content transfers, the preview transfers,
# and only at k_EItemUpdateStatusCommittingChanges does it turn into
# k_EResultInvalidParam -- which names no parameter and looks for all the world
# like a tag problem or a permissions problem. Cost a full upload to find.
$descriptionLimit = 8000
if ($description.Length -gt $descriptionLimit) {
    throw ("the description is $($description.Length) characters and Steam accepts " +
           "$descriptionLimit. It would upload everything and then fail at the commit " +
           "with k_EResultInvalidParam. Trim docs/store-description.bbcode by " +
           "$($description.Length - $descriptionLimit) characters.")
}

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
    # GetItemTask.LoadFrom walks the node's CHILDREN looking for one named
    # ItemId and reads its Value attribute -- the same shape UpdateItem uses.
    # Written as an attribute on GetItem itself the id simply never arrives, and
    # the tool dies in Convert.ToUInt64 on an empty string, having updated
    # nothing. Every update before this fix failed exactly that way.
    if ($ItemId -notmatch '^[0-9]+$') { throw "ItemId must be digits only, got '$ItemId'" }
    $tasks.Add('  <GetItem>')
    $tasks.Add('    <ItemId Value="' + (Escape-Attr $ItemId) + '" />')
    $tasks.Add('  </GetItem>')
    Write-Host "Updating existing Workshop item $ItemId."
}

$update = New-Object System.Collections.Generic.List[string]
$update.Add('  <UpdateItem>')
$update.Add('    <ModuleFolder Value="' + (Escape-Attr $module) + '" />')
$update.Add('    <ItemDescription Value="' + (Escape-Attr $description) + '" />')
$update.Add('    <Visibility Value="' + $Visibility + '" />')
# Read off the Bannerlord workshop's own filter panel. Type, Setting, Game Mode
# and Compatible Version, which is every category it sorts by.
$validTags = @(
    'Graphical Enhancement', 'Map Pack', 'Partial Conversion', 'Sound',
    'Total Conversion', 'Troops', 'UI', 'Utility', 'Weapons and Armour',
    'Native', 'Antiquity', 'Dark Ages', 'Medieval', 'Musket Era', 'Modern',
    'Sci-Fi', 'Fantasy', 'Oriental', 'Other',
    'Singleplayer', 'Multiplayer',
    'v1.2.12', 'v1.3.4', 'v1.3.5', 'v1.3.6', 'v1.3.7', 'v1.3.8', 'v1.3.9',
    'v1.3.10', 'v1.3.11', 'v1.3.12', 'v1.3.13', 'v1.3.14', 'v1.3.15',
    'v1.4.5', 'v1.4.6', 'v1.4.7', 'v1.4.8'
)
foreach ($t in $Tags) {
    if ($validTags -notcontains $t) {
        throw "Steam would silently drop the tag '$t'. Valid tags: $($validTags -join ', ')"
    }
}

$update.Add('    <Tags>')
foreach ($t in $Tags) { $update.Add('      <Tag Value="' + (Escape-Attr $t) + '" />') }
$update.Add('    </Tags>')
if (-not [string]::IsNullOrWhiteSpace($Image)) {
    # Relative to the repository, not to the shell's working directory. This
    # script is normally run from somewhere else entirely, and a path that
    # resolves for the author and not for anyone else is not a path.
    $imagePath = $Image
    if (-not [System.IO.Path]::IsPathRooted($imagePath)) {
        $imagePath = Join-Path $root $Image
    }
    if (-not (Test-Path $imagePath)) {
        throw "preview image not found: $imagePath (given as '$Image')"
    }
    $update.Add('    <Image Value="' + (Escape-Attr (Resolve-Path $imagePath)) + '" />')
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
# Read back the way the tool reads it. The id has to survive as a child of
# GetItem carrying a numeric Value, and checking that here is cheaper than
# finding out from a FormatException after the upload has not happened.
if (-not [string]::IsNullOrWhiteSpace($ItemId)) {
    $idNode = $check.SelectSingleNode('/Tasks/GetItem/ItemId')
    $idValue = if ($idNode) { $idNode.GetAttribute('Value') } else { $null }
    if ($idValue -notmatch '^[0-9]+$') {
        throw "the publisher would read the item id as '$idValue' and crash converting it"
    }
}

Write-Host "wrote $out ($seen tasks, as the publisher will read them)" -ForegroundColor Green

$version = ([xml](Get-Content (Join-Path $module "SubModule.xml"))).Module.Version.value
$name = ([xml](Get-Content (Join-Path $module "SubModule.xml"))).Module.Name.value
Write-Host ""
Write-Host "  title      $name   (from SubModule.xml, not from this file)"
Write-Host "  version    $version"
Write-Host "  module     $module"
Write-Host "  visibility $Visibility"
Write-Host ("  description " + $description.Length + " of " + $descriptionLimit + " characters")
Write-Host ("  tags       " + ($Tags -join ', '))
if ([string]::IsNullOrWhiteSpace($Image)) {
    Write-Host "  preview    none -- add one on the Workshop page afterwards" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Nexus copy (paste into the Description box):" -ForegroundColor Cyan
Write-Host "  $nexusOut"
Write-Host ""
if ($Publish) {
    Write-Host "Publishing. Steam must be running and logged in." -ForegroundColor Cyan
    Write-Host ""
    & $publisher $out
    Write-Host ""
    Write-Host "Done. Check the item page: https://steamcommunity.com/sharedfiles/filedetails/?id=$ItemId" -ForegroundColor Green
} else {
    Write-Host "Nothing published. Re-run with -Publish to send it." -ForegroundColor Yellow
}
Write-Host ""

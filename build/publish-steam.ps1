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

    # The Workshop PREVIEW: the one image in search results and browse grids,
    # which Steam shows as a square. So it is the square cover, drawn for that
    # shape, not the wide one cropped into it.
    #
    # This is the only image the publisher can send. UpdateItemTask.DoJob calls
    # SteamUGC.SetItemPreview and never AddItemPreviewFile, so the gallery in
    # docs/gallery has to be uploaded by hand on the item's web page.
    #
    # Relative paths resolve against the repository, not the working directory.
    [string]$Image = "docs/cover-square.jpg",

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

# Steam uploads the DEPLOYED module folder, and build-mod.ps1 deliberately
# refuses to copy settings.xml over one that already exists -- a deploy must
# never reset a player's configuration. Those two rules together mean the
# settings.xml that ships is whatever this machine's player last saved, and it
# drifts from the repository the moment a setting is added.
#
# It very nearly shipped that way. The deployed file was three days old and had
# no CaravanGearShare element at all, so every subscriber would have received a
# configuration file missing the mod's newest setting -- working, because
# Settings.Load falls back to the code default, but with no line to edit and no
# comment explaining it, which for a player without MCM is the difference
# between a setting and no setting.
#
# Compared by element rather than by bytes, so a comment reworded in the
# repository does not block a publish, and a value the player has tuned locally
# is reported by name instead of being silently overwritten.
$deployedSettings = Join-Path $module "settings.xml"
$sourceSettings = Join-Path $root "settings.xml"
if ((Test-Path $deployedSettings) -and (Test-Path $sourceSettings)) {
    function Get-SettingValues([string]$path) {
        $doc = New-Object System.Xml.XmlDocument
        $doc.Load($path)
        $map = @{}
        foreach ($node in $doc.DocumentElement.ChildNodes) {
            if ($node.NodeType -eq 'Element') { $map[$node.Name] = $node.InnerText.Trim() }
        }
        return $map
    }

    $deployed = Get-SettingValues $deployedSettings
    $source = Get-SettingValues $sourceSettings

    $drift = @()
    foreach ($key in ($deployed.Keys + $source.Keys | Sort-Object -Unique)) {
        $a = if ($deployed.ContainsKey($key)) { $deployed[$key] } else { '(missing)' }
        $b = if ($source.ContainsKey($key)) { $source[$key] } else { '(missing)' }
        if ($a -ne $b) { $drift += "  $key : deployed=$a  repo=$b" }
    }

    if ($drift.Count -gt 0) {
        Write-Host "the deployed settings.xml is not the one in the repository:" -ForegroundColor Red
        $drift | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        Write-Host "  Steam ships the deployed file. Copy the repository's over it" -ForegroundColor Yellow
        Write-Host "  (saving yours first if any value above is one you tuned)." -ForegroundColor Yellow
        throw "settings.xml would ship stale"
    }

    # Values agree, so nothing of the player's is at stake -- but the file can
    # still differ in its comments, and those are not decoration. Without MCM
    # they are the only explanation of a setting a player has, and they go
    # stale exactly when a mechanism changes, which is when a reader most needs
    # them right. This very publish would have shipped a paragraph describing a
    # financing multiplier that no longer exists.
    #
    # Refusing over a reworded comment would be tiresome, and silently shipping
    # a wrong one is worse. Since the values already match, the repository's
    # copy can simply be taken: it says the same thing about the player's
    # configuration and says it correctly.
    if ((Get-FileHash $deployedSettings).Hash -ne (Get-FileHash $sourceSettings).Hash) {
        Copy-Item $sourceSettings $deployedSettings -Force
        Write-Host "settings.xml: same values, refreshed the comments from the repository." -ForegroundColor Cyan
    }
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

    # The publisher's exit code is not worth reading, and this is the second
    # way it has misled. Program.ExitProgram ends with Console.ReadKey, a
    # "press any key" that has no business in a tool driven by a script: run
    # with a console it waits for a keystroke for ever and holds SubModule.xml
    # open, which once needed a reboot to clear; run with its output captured
    # it throws InvalidOperationException and exits 82 -- AFTER a completed
    # upload, and the crash is the only thing a caller sees.
    #
    # So the transcript is the witness instead. The tool prints "Uploading
    # done!" when the item has been committed, and that line is the one fact
    # worth believing about the run.
    $transcript = & $publisher $out 2>&1
    $transcript | ForEach-Object { Write-Host $_ }

    $uploaded = $transcript | Where-Object { $_ -match 'Uploading done' }
    $readKeyCrash = $transcript | Where-Object { $_ -match 'Cannot read keys' }

    Write-Host ""
    if ($uploaded) {
        if ($readKeyCrash) {
            Write-Host "The publisher crashed on exit at its 'press any key' prompt." -ForegroundColor Yellow
            Write-Host "That happens after the upload and means nothing about it." -ForegroundColor Yellow
        }
        Write-Host "Published. Check the item page: https://steamcommunity.com/sharedfiles/filedetails/?id=$ItemId" -ForegroundColor Green

        # Said out loud, because reading the transcript is only half the job.
        # PowerShell hands on the last exit code it saw, so without this the
        # script prints "Published." and then exits 82 anyway -- and a caller
        # who trusts exit codes over console output, which is the sane thing to
        # trust, still sees a failed publish.
        exit 0
    } else {
        Write-Host "The publisher never reported 'Uploading done'. Nothing was committed." -ForegroundColor Red
        Write-Host "Steam must be running, logged in, with Steam Cloud enabled for your" -ForegroundColor Yellow
        Write-Host "account and for Bannerlord. Do NOT re-run blind: check the item page" -ForegroundColor Yellow
        Write-Host "first, or a second attempt may leave you with two of something." -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "Nothing published. Re-run with -Publish to send it." -ForegroundColor Yellow
}
Write-Host ""

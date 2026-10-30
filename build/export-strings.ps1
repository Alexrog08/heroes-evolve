<#
.SYNOPSIS
  Writes every translatable string in the mod to one strings file, in the
  game's own language-file format, for a translator to start from.

.DESCRIPTION
  The English text lives in the code, after its id: "{=hev_strip}Hand over
  your arms and armour." The game shows that text whenever no language file
  carries the id, so English needs no file of its own -- but a translator
  needs one, and copying fifty strings out of four source files by hand is
  how a hint gets missed or a sentence copied half-finished.

  So this reads them out of src/ instead: every string literal that opens
  with {=hev_...}, joined with the literals it is concatenated to, which is
  how the longer dialogue lines and hints are written. An id used twice with
  two different texts is a mistake in the code, since the game can only
  translate it one way, and stops the export.

  It also compares the result against each folder in ModuleData/Languages
  and lists the ids a translation lacks (shown in English in that language)
  or still carries after the code dropped them.

.PARAMETER Out
  Where to write the file. Defaults to .tmp/hev_strings_english.xml, which
  is never committed.

.PARAMETER Language
  The language tag written into the file. A translator replaces it with the
  game's id for their language (for example 简体中文 or 繁體中文).
#>
param(
    [string]$Out,
    [string]$Language = 'English'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Out) { $Out = Join-Path $root '.tmp\hev_strings_english.xml' }

# One literal opening with an id, then any literals added to it with +.
$pattern = '"\{=(?<id>hev_[A-Za-z0-9_]+)\}(?<first>(?:[^"\\]|\\.)*)"(?:\s*\+\s*"(?<more>(?:[^"\\]|\\.)*)")*'

function Unescape([string]$s, [string]$where) {
    $plain = $s.Replace('\\', [string][char]0).Replace('\"', '"')
    if ($plain.Contains('\')) { throw "unsupported escape in $where : $s" }
    return $plain.Replace([string][char]0, '\')
}

$strings = [ordered]@{}
$files = [ordered]@{}
$conflicts = @()

foreach ($file in Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | Sort-Object FullName) {
    $code = [IO.File]::ReadAllText($file.FullName)
    $relative = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
    foreach ($m in [regex]::Matches($code, $pattern)) {
        $id = $m.Groups['id'].Value
        $text = $m.Groups['first'].Value
        foreach ($c in $m.Groups['more'].Captures) { $text += $c.Value }
        $text = Unescape $text "$relative ($id)"

        if ($strings.Contains($id)) {
            if ($strings[$id] -ne $text) { $conflicts += "$id`n    $($strings[$id])`n    $text" }
            continue
        }
        $strings[$id] = $text
        $files[$id] = $relative
    }
}

if ($conflicts.Count -gt 0) {
    Write-Host "Ids used with two different texts:" -ForegroundColor Red
    $conflicts | ForEach-Object { Write-Host "  $_" }
    exit 1
}

New-Item -ItemType Directory -Force (Split-Path -Parent $Out) | Out-Null
$xmlSettings = New-Object System.Xml.XmlWriterSettings
$xmlSettings.Indent = $true
$xmlSettings.IndentChars = '  '
$xmlSettings.Encoding = New-Object System.Text.UTF8Encoding($false)
$w = [System.Xml.XmlWriter]::Create($Out, $xmlSettings)
try {
    $w.WriteStartDocument()
    $w.WriteStartElement('base')
    $w.WriteAttributeString('xmlns', 'xsi', $null, 'http://www.w3.org/2001/XMLSchema-instance')
    $w.WriteAttributeString('xmlns', 'xsd', $null, 'http://www.w3.org/2001/XMLSchema')
    $w.WriteAttributeString('type', 'string')
    $w.WriteStartElement('tags')
    $w.WriteStartElement('tag')
    $w.WriteAttributeString('language', $Language)
    $w.WriteEndElement()
    $w.WriteEndElement()
    $w.WriteStartElement('strings')
    $last = $null
    foreach ($id in $strings.Keys) {
        if ($files[$id] -ne $last) { $w.WriteComment(' ' + $files[$id] + ' '); $last = $files[$id] }
        $w.WriteStartElement('string')
        $w.WriteAttributeString('id', $id)
        $w.WriteAttributeString('text', $strings[$id])
        $w.WriteEndElement()
    }
    $w.WriteEndElement()
    $w.WriteEndElement()
    $w.WriteEndDocument()
} finally {
    $w.Close()
}

Write-Host "$($strings.Count) strings written to $Out"

foreach ($lang in Get-ChildItem (Join-Path $root 'ModuleData\Languages') -Directory | Sort-Object Name) {
    foreach ($xml in Get-ChildItem $lang.FullName -Filter 'hev_strings_*.xml') {
        [xml]$doc = Get-Content -Raw -Encoding UTF8 $xml.FullName
        $have = @($doc.base.strings.string | ForEach-Object { $_.id })
        $missing = @($strings.Keys | Where-Object { $have -notcontains $_ })
        $stale = @($have | Where-Object { -not $strings.Contains($_) })
        $line = "  $($lang.Name): $($have.Count) translated, $($missing.Count) missing"
        if ($stale.Count -gt 0) { $line += ", $($stale.Count) no longer in the code: $($stale -join ', ')" }
        Write-Host $line
    }
}

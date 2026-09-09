<#
.SYNOPSIS
  Confirms every TaleWorlds.* member the built mod calls actually exists in
  the installed game's assemblies, before the mod ever gets to a campaign
  load screen.

.DESCRIPTION
  DynamicLordGear v1.2.2 calls MBEquipmentRoster.HasEquipmentFlags and
  IsEquipmentTemplate, both removed by v1.4.8, and dies at campaign load
  with a MissingMethodException. csc.exe happily compiled that mod, because
  it linked against a v1.4.8 install too -- the members existed when the mod
  author last built it, and nothing re-checked that assumption after the
  game patched. This script is that re-check: it reads raw ECMA-335
  metadata (no execution, no game process involved) from the shipped
  TaleWorlds*.dll set and from our own compiled output, and diffs what we
  call against what is actually there.

  Three ways an earlier, naively-written version of this same check produced
  a false "API OK" are fixed here on purpose (each verified against this
  mod's own compiled DLL, not just reasoned about):

    1. Nested type owners. A member declared on a nested type (for example
       DefaultPerks+Crossbow::get_MountedCrossbowman) has an empty
       Namespace in its metadata -- the enclosing type is reached through
       GetDeclaringType() (for a type we're indexing) or ResolutionScope
       (for a type referenced from elsewhere). A filter that only reads
       Namespace+Name silently turns the owner into the bare nested name
       ("Crossbow"), which never matches "^TaleWorlds", so the whole
       reference is dropped before it is ever compared against what
       actually exists. Fixed by walking the declaring-type / resolution-
       scope chain outward and joining with "+", the same way the CLR
       itself names nested types.

    2. Field references. WeaponComponentData.WeaponFlags is a field, not a
       property, so it appears in the reference table as plain
       "WeaponFlags" with no "get_" prefix. Enumerating only GetMethods()
       when building the set of what exists makes every field-backed
       member look missing. Fixed by enumerating both GetMethods() and
       GetFields() on both sides of the diff.

    3. Constructed generic types. A call through a generic type -- for
       this exact mod, CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener,
       which resolves to TaleWorlds.CampaignSystem.IMbEvent`1's method, not
       some non-generic type -- has a MemberReference.Parent of kind
       TypeSpecification, not TypeReference. A filter that only accepts
       TypeReference silently drops it. This is not a hypothetical: it is
       confirmed (see verify-api.negative-control notes and task-11-report.md)
       that this exact mod's HeroLoadoutBehavior wiring is a
       TypeSpecification-parented reference, so an unfixed checker would
       have waved through the one call this whole task exists to add.
       Fixed by decoding the TypeSpecification's signature (element type
       0x15 = GENERICINST, then the CLASS/VALUETYPE tag byte, then
       BlobReader.ReadTypeHandle() for the open generic type) and resolving
       that handle the same way as any other type owner -- recursively, so
       a nested type behind a generic instantiation still resolves.

  A fourth, more basic problem is also fixed: two of the installed
  TaleWorlds*.dll files (TaleWorlds.Native.dll and friends) are native PE
  images with no CLR metadata at all. Calling GetMetadataReader() on one of
  those throws ("PE image does not have metadata"), which -- with
  $ErrorActionPreference = "Stop" -- kills the whole script before it ever
  reaches the three fixes above. Guarded with PEReader.HasMetadata.

.PARAMETER NegativeControl
  A checker that can only ever say "OK" has proven nothing. This mode takes
  the real reference list this script would otherwise check, adds two
  synthetic member names that are known not to exist (one on a top-level
  type, one on a nested type), and confirms both come back flagged as
  missing. It reuses the exact same known-member index and the exact same
  diff logic as the real check -- only the input is spiked -- so a pass
  here is evidence about this script, not about a separate copy of it.
#>
param(
    [switch]$NegativeControl
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$game = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
$dll  = Join-Path $root "build\out\HeroesEvolve.dll"

if (-not (Test-Path $dll)) { Write-Host "Build the mod first."; exit 1 }

Add-Type -AssemblyName System.Reflection.Metadata | Out-Null
Add-Type -AssemblyName System.Collections.Immutable | Out-Null

$HandleKind = [System.Reflection.Metadata.HandleKind]

# Full CLR-style name of a TypeDefinition (a type declared *in* the assembly
# being scanned), walking GetDeclaringType() outward so a nested type such
# as DefaultPerks+Crossbow keeps its owner instead of losing it. Blind
# spot 1, known-side.
function Get-TypeDefName([System.Reflection.Metadata.MetadataReader]$mr, $tdHandle) {
    $td = $mr.GetTypeDefinition($tdHandle)
    $name = $mr.GetString($td.Name)
    $declaring = $td.GetDeclaringType()
    if (-not $declaring.IsNil) {
        return (Get-TypeDefName $mr $declaring) + "+" + $name
    }
    $ns = $mr.GetString($td.Namespace)
    if ($ns) { return "$ns.$name" } else { return $name }
}

# Full CLR-style name of a TypeReference (a type mentioned by, but not
# declared in, the assembly being scanned). A nested type's ResolutionScope
# points at the *enclosing type* (another TypeReference), not an assembly,
# so that case recurses instead of reading Namespace directly. Blind spot
# 1, reference-side.
function Get-TypeRefName([System.Reflection.Metadata.MetadataReader]$mr, $trHandle) {
    $tr = $mr.GetTypeReference($trHandle)
    $name = $mr.GetString($tr.Name)
    $scope = $tr.ResolutionScope
    if (-not $scope.IsNil -and $scope.Kind -eq $HandleKind::TypeReference) {
        $parentHandle = [System.Reflection.Metadata.TypeReferenceHandle]$scope
        return (Get-TypeRefName $mr $parentHandle) + "+" + $name
    }
    $ns = $mr.GetString($tr.Namespace)
    if ($ns) { return "$ns.$name" } else { return $name }
}

# Resolves any MemberReference.Parent handle to a full owner name, whatever
# kind it is. TypeReference and TypeDefinition are the ordinary cases;
# TypeSpecification is blind spot 3 -- a call through a constructed generic
# type (e.g. IMbEvent<Action<Hero>>, or MBReadOnlyList<ItemObject>) is
# neither of the plain kinds. Its signature blob is decoded far enough to
# find the *open* generic type (element type 0x15 = GENERICINST, then a
# CLASS/VALUETYPE tag byte, then the type handle itself via
# BlobReader.ReadTypeHandle()) and that handle is resolved recursively, so
# a nested type behind a generic instantiation still comes out right.
function Get-OwnerName([System.Reflection.Metadata.MetadataReader]$mr, $handle) {
    switch ($handle.Kind) {
        ($HandleKind::TypeReference) {
            return Get-TypeRefName $mr ([System.Reflection.Metadata.TypeReferenceHandle]$handle)
        }
        ($HandleKind::TypeDefinition) {
            return Get-TypeDefName $mr ([System.Reflection.Metadata.TypeDefinitionHandle]$handle)
        }
        ($HandleKind::TypeSpecification) {
            $ts = $mr.GetTypeSpecification([System.Reflection.Metadata.TypeSpecificationHandle]$handle)
            $br = $mr.GetBlobReader($ts.Signature)
            $elementType = $br.ReadByte()
            if ($elementType -eq 0x15) {
                # GENERICINST: next byte is CLASS(0x12)/VALUETYPE(0x11), then
                # the open generic type. We only need that type's identity,
                # not the type arguments, so nothing after ReadTypeHandle()
                # is read.
                $br.ReadByte() | Out-Null
                $generic = $br.ReadTypeHandle()
                return Get-OwnerName $mr $generic
            }
            # Some other TypeSpec shape (array/pointer/etc.) -- not a
            # member-call receiver we need to resolve here.
            return $null
        }
        default { return $null }
    }
}

# Every member a TaleWorlds*.dll actually declares: methods (which is where
# properties live too, as get_/set_) and fields. Blind spot 2 is fixed
# simply by asking for both.
function Get-Members([string]$path) {
    $fs = [System.IO.File]::OpenRead($path)
    try {
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($fs)
        # A handful of installed "TaleWorlds*.dll" files are native PE
        # images (no CLI header at all) and GetMetadataReader() throws on
        # them. Skipping them here is what stops the whole script dying on
        # the second problem in the whole file list.
        if (-not $pe.HasMetadata) { return }
        $mr = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        foreach ($th in $mr.TypeDefinitions) {
            $full = Get-TypeDefName $mr $th
            $td = $mr.GetTypeDefinition($th)
            foreach ($mh in $td.GetMethods()) { "$full::" + $mr.GetString($mr.GetMethodDefinition($mh).Name) }
            foreach ($fh in $td.GetFields())  { "$full::" + $mr.GetString($mr.GetFieldDefinition($fh).Name) }
        }
    } finally {
        $pe.Dispose(); $fs.Dispose()
    }
}

# Every TaleWorlds.* member our own compiled DLL references, whatever kind
# of type owns it (plain type, nested type, or constructed generic type --
# blind spot 3).
function Get-References([string]$path) {
    $fs = [System.IO.File]::OpenRead($path)
    try {
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($fs)
        if (-not $pe.HasMetadata) { return }
        $mr = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        foreach ($h in $mr.MemberReferences) {
            $m = $mr.GetMemberReference($h)
            $owner = Get-OwnerName $mr $m.Parent
            if (-not $owner) { continue }
            if ($owner -notmatch '^TaleWorlds') { continue }
            "$owner::" + $mr.GetString($m.Name)
        }
    } finally {
        $pe.Dispose(); $fs.Dispose()
    }
}

# ---- Build the "what actually exists in v1.4.8" index ----

$known = @{}
$scanned = 0
$skipped = 0
Get-ChildItem (Join-Path $game "bin\Win64_Shipping_Client") -Filter "TaleWorlds*.dll" | ForEach-Object {
    $names = Get-Members $_.FullName
    if ($null -eq $names) { $skipped++; return }
    $scanned++
    foreach ($n in $names) { $known[$n] = $true }
}
Write-Host "Scanned $scanned assemblies ($skipped skipped, no managed metadata); $($known.Count) indexed member names."

# ---- Negative control: prove the diff below can actually fail ----
#
# Two member names that are guaranteed not to exist -- one on a real
# top-level type (ItemObject), one on a real nested type
# (DefaultPerks+Crossbow), so the check is exercised on both name shapes
# that blind spot 1 used to break. They are folded into the *real*
# reference list and run through the *real* diff, so this is testing this
# script, not a separate reimplementation of it.
$bogusTopLevel = "TaleWorlds.Core.ItemObject::get_ThisMemberDoesNotExist_HLF"
$bogusNested   = "TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks+Crossbow::get_NoSuchPerk_HLF"

if ($NegativeControl) {
    $refs = @(Get-References $dll) + @($bogusTopLevel, $bogusNested)
    $missing = $refs | Sort-Object -Unique | Where-Object { -not $known.ContainsKey($_) }

    $caughtTopLevel = [bool]($missing -contains $bogusTopLevel)
    $caughtNested   = [bool]($missing -contains $bogusNested)
    $otherMissing   = @($missing | Where-Object { $_ -ne $bogusTopLevel -and $_ -ne $bogusNested })

    Write-Host "NEGATIVE CONTROL: injected one top-level and one nested member known not to exist."
    Write-Host "  top-level bogus member ($bogusTopLevel) flagged missing: $caughtTopLevel"
    Write-Host "  nested bogus member ($bogusNested) flagged missing:      $caughtNested"
    if ($otherMissing.Count -gt 0) {
        Write-Host "  (also genuinely missing, independent of the injected pair:)"
        $otherMissing | ForEach-Object { Write-Host "    $_" }
    }

    if ($caughtTopLevel -and $caughtNested) {
        Write-Host "NEGATIVE CONTROL PASSED: a member that does not exist is correctly reported as missing, top-level and nested alike."
        exit 0
    }
    Write-Host "NEGATIVE CONTROL FAILED: the checker let at least one known-bad member pass. Do not trust an OK from this script until this is fixed."
    exit 1
}

# ---- The real check ----

$missing = Get-References $dll | Sort-Object -Unique | Where-Object { -not $known.ContainsKey($_) }

if ($missing) {
    Write-Host "MISSING FROM v1.4.8:"
    $missing | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host "API OK: every TaleWorlds member referenced exists in v1.4.8"
exit 0

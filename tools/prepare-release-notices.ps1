[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('windows', 'linux', 'macos')]
    [string] $Platform,
    [Parameter(Mandatory)]
    [string] $OutputDirectory,
    [Parameter(Mandatory)]
    [string] $Version,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string] $SourceCommit,
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string] $Repository = 'cdd1037/san9-remake'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$rid = @{ windows = 'win-x64'; linux = 'linux-x64'; macos = 'osx-arm64' }[$Platform]
$buildRoot = Join-Path $repoRoot 'src/San9.Client.Godot/.godot/mono/temp'
$deps = Get-Content -LiteralPath "$buildRoot/bin/ExportRelease/$rid/San9.Client.Godot.deps.json" -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
$assets = Get-Content -LiteralPath "$buildRoot/obj/project.assets.json" -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
$notices = [Collections.Generic.List[string]]::new()
$notices.Add('San9R third-party licenses and notices. Godot and font notices are also provided in GODOT-LICENSE.txt, GODOT-COPYRIGHT.txt and OFL.txt.')
$runtimeFound = $false

function Add-Notice([string] $Title, [string] $Path) {
    $text = Get-Content -LiteralPath $Path -Raw -Encoding utf8
    $notices.Add("`n===== $Title =====`n`n$text")
}

function Get-PackageRoot([string] $Path) {
    foreach ($folder in $assets.packageFolders.Keys) {
        $candidate = Join-Path $folder $Path
        if (Test-Path -LiteralPath $candidate -PathType Container) { return $candidate }
    }
    throw "Missing restored package: $Path"
}

# Read the exported dependency graph, including transitive packages and the exact runtime.
# New or upgraded dependencies must have their notices reviewed before publication.
foreach ($key in ($deps.libraries.Keys | Sort-Object)) {
    $library = $deps.libraries[$key]
    if ($library.type -eq 'project') { continue }
    if ($key.StartsWith("runtimepack.Microsoft.NETCore.App.Runtime.$rid/")) {
        $package = Get-PackageRoot ($key.Substring('runtimepack.'.Length).ToLowerInvariant())
        Add-Notice "$key / LICENSE.TXT" "$package/LICENSE.TXT"
        Add-Notice "$key / THIRD-PARTY-NOTICES.TXT" "$package/THIRD-PARTY-NOTICES.TXT"
        $runtimeFound = $true
        continue
    }
    switch ($key) {
        'GodotSharp/4.7.2' {
            $notices.Add("`n${key}: see GODOT-LICENSE.txt and GODOT-COPYRIGHT.txt.")
        }
        'Cdd.FaceDetection/0.1.0-preview.4' {
            $package = Get-PackageRoot $library.path
            foreach ($file in @('LICENSE', 'THIRD-PARTY-NOTICES.txt', 'licenses/libfacedetection/LICENSE',
                'licenses/libfacedetection/MODEL-NOTICE.txt', 'licenses/libfacedetection/SOURCE-NOTICE.txt',
                'licenses/libfacedetection/UPSTREAM.txt')) {
                Add-Notice "$key / $file" (Join-Path $package $file)
            }
        }
        'OpenccNetLib/1.6.2' {
            Add-Notice "$key / LICENSE" "$repoRoot/licenses/OpenccNetLib-1.6.2/LICENSE.txt"
            Add-Notice "$key / THIRD_PARTY_NOTICES" "$repoRoot/licenses/OpenccNetLib-1.6.2/THIRD-PARTY-NOTICES.txt"
            $package = Get-PackageRoot $library.path
            Add-Notice "$key / OpenCC dictionaries / Apache-2.0" "$package/contentFiles/any/any/dicts/LICENSE.txt"
        }
        { $_ -in @('PeterO.Cbor/4.5.5', 'PeterO.Numbers/1.8.2', 'PeterO.URIUtility/1.0.0') } {
            Add-Notice "$key / Peter Occil / CC0-1.0" "$repoRoot/licenses/CC0-1.0.txt"
        }
        'ZstdSharp.Port/0.8.8' {
            Add-Notice "$key / LICENSE" "$repoRoot/licenses/ZstdSharp.Port-0.8.8/LICENSE.txt"
            Add-Notice "$key / Zstandard 1.5.7 / BSD license" "$repoRoot/licenses/ZstdSharp.Port-0.8.8/ZSTD-LICENSE.txt"
        }
        default { throw "Review and register release notices for dependency: $key" }
    }
}
if (-not $runtimeFound) { throw "The export dependency graph has no .NET runtime for $rid." }

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Copy-Item -LiteralPath "$repoRoot/LICENSE" -Destination "$OutputDirectory/LICENSE.txt"
$encoding = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'THIRD-PARTY-NOTICES.txt'), ($notices -join "`n"), $encoding)
$source = @"
San9R $Version
License: GPL-3.0-only (see LICENSE.txt).
Source commit: $SourceCommit
Source: https://github.com/$Repository/tree/$SourceCommit
Source archive: https://github.com/$Repository/archive/$SourceCommit.tar.gz
Build instructions: README.md; tools/setup-release.ps1; tools/export-client.ps1; .github/workflows/release.yml in that source tree.
Original game files and converted game content are not included.
"@
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SOURCE.txt'), "$source`n", $encoding)

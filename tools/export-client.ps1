[CmdletBinding()]
param(
    [ValidateSet('windows', 'linux', 'macos')]
    [string] $Platform = 'windows',
    [string] $GodotPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectRoot = Join-Path $repoRoot 'src/San9.Client.Godot'
$target = @{
    windows = @{ Preset = 'Windows Desktop'; Directory = 'windows-x64'; File = 'San9.exe'; Template = 'windows_release_x86_64.exe' }
    linux = @{ Preset = 'Linux'; Directory = 'linux-x64'; File = 'San9.x86_64'; Template = 'linux_release.x86_64' }
    macos = @{ Preset = 'macOS'; Directory = 'macos-arm64'; File = 'San9.zip'; Template = 'macos_release.arm64.mono.zip' }
}[$Platform]
$outputRoot = Join-Path $repoRoot "local/generated/export/$($target.Directory)"
$template = Join-Path $repoRoot "local/generated/release-tools/templates/4.7.2-slim.2/$($target.Template)"

if (-not $GodotPath) {
    $editor = Get-Command 'Godot*_mono_win64.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $editor) { throw 'Pass -GodotPath with the Godot 4.7.2 .NET executable returned by tools/setup-release.ps1.' }
    $GodotPath = $editor.Source
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
foreach ($required in @($template,
    (Join-Path $projectRoot 'assets/LXGWWenKaiMonoGBLite-Medium.ttf'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing export input: $required" }
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

function Invoke-Godot([string[]] $Arguments, [string] $LogName) {
    $stdout = Join-Path $outputRoot "$LogName.log"
    $stderr = Join-Path $outputRoot "$LogName.errors.log"
    $commandArguments = @('--headless', '--path', ('"' + $projectRoot + '"')) + $Arguments
    $startOptions = @{
        FilePath = $GodotPath
        ArgumentList = $commandArguments
        PassThru = $true
        RedirectStandardOutput = $stdout
        RedirectStandardError = $stderr
    }
    if ($IsWindows) { $startOptions.WindowStyle = 'Hidden' }
    $process = Start-Process @startOptions
    # Wait for the editor, not its reusable MSBuild worker processes.
    $process.WaitForExit()
    Get-Content -LiteralPath $stdout -Encoding utf8 | Write-Host
    Get-Content -LiteralPath $stderr -Encoding utf8 | Write-Host
    if ($process.ExitCode -ne 0) { throw "Godot $LogName failed ($($process.ExitCode)). See $stdout and $stderr." }
    if (Select-String -LiteralPath $stdout, $stderr -Pattern '^ERROR:' -Quiet) {
        throw "Godot $LogName reported errors despite a zero exit code. See $stdout and $stderr."
    }
}

Push-Location $repoRoot
try {
    Invoke-Godot @('--import') 'import'
    $output = Join-Path $outputRoot $target.File
    Invoke-Godot @('--export-release', ('"' + $target.Preset + '"'), ('"' + $output + '"')) 'export'
    if (-not (Test-Path -LiteralPath $output -PathType Leaf)) { throw "Godot did not produce $output." }

    Write-Host "Exported: $output"
    Write-Host 'The export includes the project and .NET publish outputs. Converted content is not copied.'
} finally {
    Pop-Location
}

[CmdletBinding()]
param([string]$ReleasePath = "artifacts\release")

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedReleasePath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ReleasePath))
if (-not $resolvedReleasePath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "ReleasePath must resolve inside the repository."
}
if (-not (Test-Path -LiteralPath $resolvedReleasePath)) {
    throw "Release directory '$resolvedReleasePath' does not exist."
}

$checksumPath = Join-Path $resolvedReleasePath "SHA256SUMS.txt"
$lines = Get-ChildItem -LiteralPath $resolvedReleasePath -File |
    Where-Object Name -ne "SHA256SUMS.txt" |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    }
if ($lines.Count -eq 0) { throw "No release artifacts were found to checksum." }
[IO.File]::WriteAllLines($checksumPath, [string[]]$lines, [Text.UTF8Encoding]::new($false))
Write-Output $checksumPath

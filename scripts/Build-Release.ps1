[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$ArtifactsPath = "artifacts",
    [string]$ExpectedVersion,
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repositoryRoot "src\ControllerBattery\ControllerBattery.csproj"
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ArtifactsPath))
if (-not $artifactsRoot.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "ArtifactsPath must resolve inside the repository."
}

[xml]$project = Get-Content -LiteralPath $projectPath -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "The project does not declare a Version."
}
if ($ExpectedVersion -and $ExpectedVersion -ne $version) {
    throw "Release version '$ExpectedVersion' does not match project version '$version'."
}

$publishPath = Join-Path $artifactsRoot "publish\$RuntimeIdentifier"
$portableStagePath = Join-Path $artifactsRoot "stage\ControllerBattery-$version-$RuntimeIdentifier"
$releasePath = Join-Path $artifactsRoot "release"
foreach ($path in @($publishPath, $portableStagePath, $releasePath)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

if (-not $NoRestore) {
    dotnet restore $projectPath --runtime $RuntimeIdentifier
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }
}

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --no-restore `
    -p:PublishProfile=win-x64 `
    -p:ContinuousIntegrationBuild=true `
    --output $publishPath
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

Copy-Item -Path (Join-Path $publishPath "*") -Destination $portableStagePath -Recurse
Copy-Item -LiteralPath (Join-Path $repositoryRoot "README.md") -Destination $portableStagePath
Copy-Item -LiteralPath (Join-Path $repositoryRoot "CHANGELOG.md") -Destination $portableStagePath
$licensePath = Join-Path $repositoryRoot "LICENSE"
if (Test-Path -LiteralPath $licensePath) {
    Copy-Item -LiteralPath $licensePath -Destination $portableStagePath
}

$portableArchive = Join-Path $releasePath "ControllerBattery-$version-$RuntimeIdentifier-portable.zip"
Compress-Archive -Path (Join-Path $portableStagePath "*") -DestinationPath $portableArchive `
    -CompressionLevel Optimal

[pscustomobject]@{
    Version = $version
    RuntimeIdentifier = $RuntimeIdentifier
    PublishPath = $publishPath
    ReleasePath = $releasePath
    PortableArchive = $portableArchive
}

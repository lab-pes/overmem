param(
    [string]$OutputPath = 'artifacts/distribution',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
# Both hosts share dependencies and profiles in one relocatable directory.
foreach ($project in @('Overmem.Cli', 'Overmem.McpServer')) {
    dotnet publish (Join-Path $repoRoot "src/$project/$project.csproj") -c $Configuration --self-contained false -o $outputDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw "Publication failed for $project" }
}
Write-Output "Overmem CLI and MCP server: $outputDirectory"

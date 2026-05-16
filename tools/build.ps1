param(
    [string]$Configuration = "Release"
)

$projectRoot = Split-Path -Parent $PSScriptRoot
$srcProject = Join-Path $projectRoot "src\CatastropheContract.csproj"

Write-Host "Building Catastrophe Contract from $srcProject"
Write-Host "This script expects a working .NET SDK and Godot export toolchain."
Write-Host "Example:"
Write-Host "  dotnet build `"$srcProject`" -c $Configuration"
Write-Host "  godot --headless --path `"$projectRoot\godot`" --export-pack Windows `"$projectRoot\Catastrophe Contract.pck`""

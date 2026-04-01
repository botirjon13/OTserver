param(
    [string]$Version = "1.2.22",
    [string]$Runtime = "win-x64",
    [string]$Channel = "stable",
    [string]$OutputDir = "ReleaseBundles\velopack"
)

$ErrorActionPreference = "Stop"

$project = "SantexnikaSRM.csproj"
$publishDir = "tmp_publish_velopack"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish $project -c Release -r $Runtime --self-contained false -p:PublishSingleFile=false -o $publishDir

if (!(Test-Path $OutputDir)) {
    New-Item -Path $OutputDir -ItemType Directory | Out-Null
}

dotnet tool restore
dotnet tool run vpk pack `
  --packId "OsontrackSRM" `
  --packVersion $Version `
  --packDir $publishDir `
  --mainExe "SantexnikaSRM.exe" `
  --channel $Channel `
  --outputDir $OutputDir

Write-Host "Velopack package created in: $OutputDir"

param(
    [string]$Version = "1.2.41",
    [string]$Runtime = "win-x64",
    [string]$Channel = "stable",
    [string]$OutputDir = "ReleaseBundles\velopack"
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version '$Version' noto'g'ri. Misol: 1.2.41"
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $projectRoot "SantexnikaSRM.csproj"
$publishDir = Join-Path $projectRoot "tmp_publish_velopack"
$outputPath = if ([System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir
} else {
    Join-Path $projectRoot $OutputDir
}

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

New-Item -Path $outputPath -ItemType Directory -Force | Out-Null

dotnet publish $project `
  -c Release `
  -r $Runtime `
  --self-contained false `
  -p:EnableWindowsTargeting=true `
  -p:PublishSingleFile=false `
  "-p:Version=$Version" `
  "-p:AssemblyVersion=$($Version).0" `
  "-p:FileVersion=$($Version).0" `
  "-p:InformationalVersion=$Version" `
  -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish muvaffaqiyatsiz tugadi."
}

Push-Location $projectRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore muvaffaqiyatsiz tugadi."
    }

   dotnet tool run vpk pack `
     --packId "OsontrackSRM" `
     --packVersion $Version `
     --packDir $publishDir `
     --mainExe "SantexnikaSRM.exe" `
     --channel $Channel `
     --outputDir $outputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Velopack package yaratish muvaffaqiyatsiz tugadi."
    }
}
finally {
    Pop-Location
}

Write-Host "Velopack package created in: $outputPath"

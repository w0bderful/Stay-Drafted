param([string]$GameDir='D:/SteamLibrary/steamapps/common/RimWorld',[string]$WorkshopDir='D:/SteamLibrary/steamapps/workshop/content/294100')
$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
& "$PSScriptRoot/../Source/build.ps1" -GameDir $GameDir -WorkshopDir $WorkshopDir
dotnet build "$PSScriptRoot/Validate.csproj" -v q "-p:GameDir=$GameDir" "-p:WorkshopDir=$WorkshopDir"
if($LASTEXITCODE -ne 0){throw 'Test build failed'}
uv run --no-project python "$PSScriptRoot/run-game-runtime.py" $workspace $GameDir $WorkshopDir
if($LASTEXITCODE -ne 0){throw 'Validation failed'}

param([string]$GameDir = 'D:/SteamLibrary/steamapps/common/RimWorld', [string]$WorkshopDir = 'D:/SteamLibrary/steamapps/workshop/content/294100')
$ErrorActionPreference = 'Stop'
& dotnet build "$PSScriptRoot/HoldDraft.csproj" -c Release -nologo -v q "-p:GameDir=$GameDir" "-p:WorkshopDir=$WorkshopDir"
if ($LASTEXITCODE -ne 0) { throw 'Stay Drafted build failed' }

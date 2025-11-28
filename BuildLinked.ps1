# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/LHP2_Archi_Mod/*" -Force -Recurse
dotnet publish "./LHP2_Archi_Mod.csproj" -c Release -o "$env:RELOADEDIIMODS/LHP2_Archi_Mod" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location
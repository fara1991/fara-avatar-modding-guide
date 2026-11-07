param(
  [Parameter(Mandatory = $true)][string]$Project,   # e.g. E:\Projects\fara\Temp\shots-unity\vrc-demo
  [string]$VccTemplate = "$env:LOCALAPPDATA\Programs\VRChat Creator Companion\Templates\Avatar",
  [string]$VccRepos = "$env:LOCALAPPDATA\VRChatCreatorCompanion\Repos"
)
# ASCII only (Windows PowerShell 5.1 reads this file in the ANSI code page).
# Creates a NEW VRChat avatar project from the VCC template, extracting the packages from VCC's local cache
# (the same files VCC would extract). Does not touch any existing project.
$ErrorActionPreference = 'Stop'
if (Test-Path $Project) { throw "Already exists: $Project" }
New-Item -ItemType Directory -Force -Path $Project | Out-Null
foreach ($d in 'Assets', 'Packages', 'ProjectSettings') { Copy-Item -Recurse (Join-Path $VccTemplate $d) (Join-Path $Project $d) }

# name:version  (versions used for the site's articles; change them to match the article being updated)
$packages = @(
  'com.vrchat.base:3.10.6', 'com.vrchat.avatars:3.10.6', 'nadena.dev.ndmf:1.11.0',
  'nadena.dev.modular-avatar:1.16.2', 'com.anatawa12.avatar-optimizer:1.9.21', 'com.hhotatea.avatar-pose-library:1.2.44'
)
foreach ($spec in $packages) {
  $name, $ver = $spec.Split(':')
  $zip = Join-Path $VccRepos "$name\$name-$ver.zip"
  if (-not (Test-Path $zip)) { throw "Not in the VCC cache: $zip" }
  Expand-Archive -Path $zip -DestinationPath (Join-Path $Project "Packages\$name") -Force
}

# Unity registry dependencies of the packages (VCC adds these to Packages/manifest.json)
$manifestPath = Join-Path $Project 'Packages\manifest.json'
$m = Get-Content -Raw $manifestPath | ConvertFrom-Json
$m.dependencies | Add-Member -NotePropertyName 'com.unity.nuget.newtonsoft-json' -NotePropertyValue '3.2.1' -Force
$m.dependencies | Add-Member -NotePropertyName 'com.unity.burst' -NotePropertyValue '1.8.7' -Force
$m | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 $manifestPath

# Capture script (runs inside Unity when go.flag exists)
New-Item -ItemType Directory -Force -Path (Join-Path $Project 'Assets\Editor') | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'ShotRunner.cs') (Join-Path $Project 'Assets\Editor\ShotRunner.cs')
"Created $Project"

param(
 [Parameter(Mandatory=$true)][string]$GamePath,
 [Parameter(Mandatory=$true)][string]$SyncLib,
 [Parameter(Mandatory=$true)][string]$UnityEditor,
 [string]$Bundle=(Join-Path $PSScriptRoot '../../Builds/PortalGun/portalgun-models'),
 [string]$Output=(Join-Path $PSScriptRoot '../../Builds/Native')
)
$ErrorActionPreference='Stop'
$compiler=Join-Path $UnityEditor 'Data/Tools/Roslyn/csc.exe'
foreach($path in @($compiler,$SyncLib,$Bundle)){if(!(Test-Path -LiteralPath $path)){throw "Missing input: $path"}}
foreach($role in @('Client','Server')) {
 $folder=Join-Path $Output $role
 New-Item -ItemType Directory -Force -Path $folder | Out-Null
 $arguments=@('/nologo','/target:library','/langversion:8','/nostdlib+','/warnaserror+','/optimize+',('/define:'+$role.ToUpperInvariant()))
 $arguments+='/out:"'+(Join-Path $folder 'PortalGun.dll')+'"'
 $arguments+=Get-ChildItem -LiteralPath (Join-Path $GamePath 'A Township Tale_Data\Managed') -Filter '*.dll' | ForEach-Object {'/reference:"'+$_.FullName+'"'}
 foreach($ref in @((Join-Path $GamePath 'MelonLoader\net35\MelonLoader.dll'),(Join-Path $GamePath 'MelonLoader\net35\0Harmony.dll'),$SyncLib)){$arguments+='/reference:"'+$ref+'"'}
 $arguments+='/resource:"'+$Bundle+'",PortalGun.Models'
 $arguments+=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object {'"'+$_.FullName+'"'}
 $arguments+='"'+(Join-Path $PSScriptRoot '../../Assets/PortalGun/Scripts/PortalView.cs')+'"'
 $arguments+='"'+(Join-Path $PSScriptRoot '../../Assets/PortalGun/Scripts/PortalTransitMath.cs')+'"'
 $arguments+='"'+(Join-Path $PSScriptRoot '../../Assets/PortalGun/Scripts/PortalViewLeveling.cs')+'"'
 $rsp=Join-Path $folder 'build.rsp'
 Set-Content -LiteralPath $rsp -Value $arguments
 & $compiler ('@'+$rsp)
 if($LASTEXITCODE -ne 0){throw "$role compilation failed"}
}
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Output 'Client/PortalGun.dll'),(Join-Path $Output 'Server/PortalGun.dll') | ForEach-Object { $_.Hash + '  ' + (Split-Path (Split-Path $_.Path) -Leaf) + '/PortalGun.dll' } | Set-Content (Join-Path $Output 'SHA256SUMS.txt')

param([string]$OutputDirectory,[string]$GameBuildDirectory,[string]$VsInstallDirectory,[string]$CrtDirectory)
$ErrorActionPreference='Stop'
if(-not $OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot 'out/BeatlesRockBand-PC'}
if(-not $GameBuildDirectory){$GameBuildDirectory=Join-Path $PSScriptRoot 'out/build/game'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$GameBuildDirectory=[IO.Path]::GetFullPath($GameBuildDirectory)
$binaries=@('beatlesrockband-guitar.exe','rexruntime.dll','rexgpu-xenos.dll','beatles-input.dll','beatles-content.exe')
foreach($name in $binaries){if(-not (Test-Path -LiteralPath (Join-Path $GameBuildDirectory $name))){throw ('Missing native build file: '+$name+'. Run Build-Game.ps1 or pass -GameBuildDirectory.')}}
if(-not $CrtDirectory){
 . (Join-Path $PSScriptRoot 'scripts/DevTools.ps1')
 $tools=Get-TbrbDevTools -VsInstallDirectory $VsInstallDirectory
 $redist=Join-Path $tools.Vs 'VC/Redist/MSVC'
 $versions=@(Get-ChildItem -LiteralPath $redist -Directory | Where-Object {$_.Name -match '^\d+\.\d+\.\d+$'} | Sort-Object {[version]$_.Name} -Descending)
 foreach($version in $versions){$candidate=Join-Path $version.FullName 'x64/Microsoft.VC143.CRT';if(Test-Path -LiteralPath (Join-Path $candidate 'vcruntime140.dll')){$CrtDirectory=$candidate;break}}
 if(-not $CrtDirectory){throw 'Visual C++ x64 redistributable files not found; pass -CrtDirectory.'}
}
$runtime=Join-Path $OutputDirectory 'runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$src=Join-Path $PSScriptRoot 'launcher-src'
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(-not (Test-Path -LiteralPath $csc)){throw 'The .NET Framework 4.x C# compiler was not found.'}
$compilerArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',('/out:'+(Join-Path $OutputDirectory 'BeatlesRockBandLauncher.exe')),('/win32manifest:'+(Join-Path $src 'launcher.manifest')),('/win32icon:'+(Join-Path $src 'launcher.ico')),'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Runtime.Serialization.dll')
foreach($name in @('LauncherCore.cs','LauncherApp.cs','Controls.cs','ControlsForm.cs','AeroTheme.cs','ControllerDevices.cs','Responsive.cs','DlcInstaller.cs','DlcForm.cs')){$compilerArgs+=Join-Path $src $name}
$compilerArgs+='/resource:'+(Join-Path $src 'assets/DMSerifDisplay-Regular.ttf')+',BeatlesLauncher.Display.ttf'
& $csc @compilerArgs
if($LASTEXITCODE){throw 'Launcher compilation failed.'}
foreach($name in $binaries){Copy-Item -LiteralPath (Join-Path $GameBuildDirectory $name) -Destination $runtime -Force}
foreach($dll in Get-ChildItem -LiteralPath $CrtDirectory -File -Filter '*.dll'){Copy-Item -LiteralPath $dll.FullName -Destination $runtime -Force}
foreach($name in @('launcher.ico','game-files.json')){Copy-Item -LiteralPath (Join-Path $src $name) -Destination $OutputDirectory -Force}
$lines=@('# Portable defaults. Data paths are supplied by the launcher.')+@(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'beatlesrockband.toml') -Encoding UTF8 | Where-Object {$_ -notmatch '^\s*(game_data_root|user_data_root|cache_root|log_file)\s*=' -and $_ -notmatch '^# Portable defaults'})
[IO.File]::WriteAllText((Join-Path $runtime 'beatlesrockband.toml'),(($lines -join [Environment]::NewLine)+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
$licenses=Join-Path $OutputDirectory 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
foreach($name in @('ReXGlue-LICENSE.txt','SDL3-LICENSE.txt','DMSerifDisplay-OFL.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('licenses/'+$name)) -Destination $licenses -Force}
Copy-Item -LiteralPath (Join-Path $src 'README-English.md') -Destination (Join-Path $OutputDirectory 'LEEME.md') -Force
Write-Output ('Portable application: '+$OutputDirectory)

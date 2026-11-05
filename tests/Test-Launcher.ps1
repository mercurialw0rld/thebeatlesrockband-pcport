param([string]$OutputDirectory,[string]$FixtureDirectory,[string]$GameBuildDirectory,[string]$VsInstallDirectory,[string]$CrtDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path ([IO.Path]::GetTempPath()) ('BeatlesRockBand-Tests-'+[Guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
# Tests write fake settings beside their own executable. A fresh folder keeps
# the suite from overwriting a player's installed profiles.
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new output directory for the tests.'}
& (Join-Path $projectRoot 'Build-Launcher.ps1') -OutputDirectory $OutputDirectory -GameBuildDirectory $GameBuildDirectory -VsInstallDirectory $VsInstallDirectory -CrtDirectory $CrtDirectory
if($LASTEXITCODE){throw 'Launcher build failed'}
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$names=@('LauncherControlsTests','ControllerMappingTests','InstrumentUiTests','DlcUiTests','AeroPanePaintTests','PerformanceModeTests')
if($FixtureDirectory){$names+=@('DlcImporterTests','DlcLongPathTests')}
foreach($name in $names){
 $exe=Join-Path $OutputDirectory ($name+'.exe')
 & $csc /nologo /target:exe /platform:x64 /codepage:65001 ('/out:'+$exe) ('/reference:'+(Join-Path $OutputDirectory 'BeatlesRockBandLauncher.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll (Join-Path $PSScriptRoot ($name+'.cs'))
 if($LASTEXITCODE){throw "Test build failed: $name"}
 Copy-Item -LiteralPath (Join-Path $OutputDirectory 'BeatlesRockBandLauncher.exe.config') -Destination ($exe+'.config') -Force
 $testArgument=if($name -in @('DlcImporterTests','DlcLongPathTests')){$FixtureDirectory}else{Join-Path $OutputDirectory 'previews'}
 & $exe $testArgument | Out-File -LiteralPath (Join-Path $OutputDirectory ($name+'.log')) -Encoding utf8
 if($LASTEXITCODE){throw "Test failed: $name; see $OutputDirectory"}
 Get-Content -LiteralPath (Join-Path $OutputDirectory ($name+'.log')) -Tail 1
}
Write-Output ('Results and previews: '+$OutputDirectory)

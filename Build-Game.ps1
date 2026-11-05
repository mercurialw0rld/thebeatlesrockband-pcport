param(
 [string]$GameDirectory,
 [string]$SdkDirectory,
 [string]$BuildDirectory,
 [string]$VsInstallDirectory,
 [ValidateRange(1,128)][int]$Jobs=3,
 [switch]$GenerateOnly,
 [switch]$ConfigureOnly,
 [switch]$AudioTests,
 [switch]$DlcTests
)
$ErrorActionPreference='Stop'
if(-not $SdkDirectory){$SdkDirectory=Join-Path $PSScriptRoot 'external/rexglue-sdk/out/install/tbrb'}
if(-not $BuildDirectory){$BuildDirectory=Join-Path $PSScriptRoot 'out/build/game'}
$sdk=[IO.Path]::GetFullPath($SdkDirectory)
$build=[IO.Path]::GetFullPath($BuildDirectory)
$generator=Join-Path $sdk 'bin/rexglue.exe'
if(-not (Test-Path -LiteralPath $generator)){throw 'Build the patched SDK with Build-Sdk.ps1 first, or pass -SdkDirectory.'}
$manifest=Join-Path $PSScriptRoot 'beatlesrockband_manifest.local.toml'
if($GameDirectory){
 $game=(Resolve-Path -LiteralPath $GameDirectory).Path
 $xex=Join-Path $game 'default.xex'
 if(-not (Test-Path -LiteralPath $xex)){throw 'The extracted disc folder must contain default.xex.'}
 $expected='A47CBAFBC7FC81E4E6CC9BD3CE317DB5EAA9E57FE46AA5EBE09E153C37CA898B'
 if((Get-FileHash -LiteralPath $xex -Algorithm SHA256).Hash -ne $expected){throw 'This XEX revision is not supported. See README.md for the expected hash.'}
 $gameValue=ConvertTo-Json -InputObject $game.Replace('\','/') -Compress
 $xexValue=ConvertTo-Json -InputObject $xex.Replace('\','/') -Compress
 $text=@"
[project]
name = "beatlesrockband"
sdk_version = "0.10.0"
game_root = $gameValue

[entrypoint]
file_path = $xexValue
out_directory_path = "generated/default"
includes = ["config/codegen.toml", "config/pointers.toml"]
"@
 [IO.File]::WriteAllText($manifest,$text+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
}
if(-not (Test-Path -LiteralPath $manifest)){throw 'Pass -GameDirectory with the folder of your legally obtained, extracted disc on the first build.'}
# Bootstrap before CMake configures its list of generated translation units.
# Keep code generation and its local paths out of the Git repository.
Push-Location $PSScriptRoot
try { & $generator codegen $manifest; if($LASTEXITCODE){throw 'ReXGlue code generation failed.'} } finally {Pop-Location}
if(-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'generated/default/sources.cmake'))){throw 'Code generation did not produce sources.cmake.'}
if($GenerateOnly){Write-Output 'Generated local C++ from the supplied XEX. Do not commit generated/default.';exit 0}
. (Join-Path $PSScriptRoot 'scripts/DevTools.ps1')
$tools=Get-TbrbDevTools -VsInstallDirectory $VsInstallDirectory
$audio=if($AudioTests){'ON'}else{'OFF'}
$dlc=if($DlcTests){'ON'}else{'OFF'}
& $tools.CMake -S $PSScriptRoot -B $build -G Ninja "-DCMAKE_MAKE_PROGRAM=$($tools.Ninja)" "-DCMAKE_CXX_COMPILER=$($tools.ClangCl)" -DCMAKE_CXX_COMPILER_TARGET=x86_64-pc-windows-msvc -DCMAKE_BUILD_TYPE=Release "-Drexglue_DIR=$sdk/lib/cmake/rexglue" "-DBEATLES_CODEGEN_MANIFEST=$manifest" "-DBEATLES_BUILD_AUDIO_TESTS=$audio" "-DBEATLES_BUILD_DLC_TESTS=$dlc"
if($LASTEXITCODE){throw 'Game CMake configuration failed.'}
if($ConfigureOnly){Write-Output ('Configured native build: '+$build);exit 0}
& $tools.CMake --build $build --parallel $Jobs
if($LASTEXITCODE){throw 'Native game build failed.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'beatlesrockband.toml') -Destination $build -Force
Write-Output ('Native build: '+$build)

param([switch]$Original,[string]$SdkSourceDirectory,[string]$OutputDirectory,[string]$Python='python',[string]$VsInstallDirectory)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
if(-not $SdkSourceDirectory){$SdkSourceDirectory=if($Original){Join-Path $project 'external/rexglue-sdk-clean'}else{Join-Path $project 'external/rexglue-sdk'}}
if(-not $OutputDirectory){$OutputDirectory=Join-Path ([IO.Path]::GetTempPath()) ('Tbrb-Submission-'+[Guid]::NewGuid().ToString('N'))}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a fresh test output directory'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$cpp=Join-Path $OutputDirectory 'submission.cpp'
& $Python (Join-Path $PSScriptRoot 'Make-SceneSubmissionTest.py') (Join-Path $SdkSourceDirectory 'src/graphics/d3d12/pipeline_cache.cpp') $cpp
if($LASTEXITCODE){exit $LASTEXITCODE}
. (Join-Path $project 'scripts/DevTools.ps1')
$tools=Get-TbrbDevTools -VsInstallDirectory $VsInstallDirectory
$exe=Join-Path $OutputDirectory 'submission.exe'
& $tools.ClangCl --target=x86_64-pc-windows-msvc /nologo /EHsc /O2 /MD /clang:-std=c++23 $cpp ('/Fe:'+$exe) ('/Fo:'+(Join-Path $OutputDirectory 'submission.obj'))
if($LASTEXITCODE){exit $LASTEXITCODE}
& $exe
if($Original){if($LASTEXITCODE -ne 1){throw 'Expected original code to reproduce the blocking regression'};Write-Output 'Old blocking regression reproduced';exit 0}
exit $LASTEXITCODE

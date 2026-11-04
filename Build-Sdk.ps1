param([ValidateRange(1,128)][int]$Jobs=3,[string]$SdkSourceDirectory,[string]$SdkInstallDirectory,[string]$VsInstallDirectory,[switch]$VerifyOnly)
$ErrorActionPreference='Stop'
$sdk=if($SdkSourceDirectory){[IO.Path]::GetFullPath($SdkSourceDirectory)}else{Join-Path $PSScriptRoot 'external/rexglue-sdk'}
if(-not $SdkInstallDirectory){$SdkInstallDirectory=Join-Path $sdk 'out/install/tbrb'}
$install=[IO.Path]::GetFullPath($SdkInstallDirectory)
$commit='bd833a2ab69d305f99ddf57418fc38ffec8af93d'
$patches=@('0001-tbrb-graphics-pacing.patch','0002-nonblocking-pipeline-submission.patch') | ForEach-Object {Join-Path $PSScriptRoot ('sdk-patches/'+$_)}
$git=(Get-Command git -ErrorAction Stop).Source
if(-not (Test-Path -LiteralPath (Join-Path $sdk '.git'))){
 if($VerifyOnly){throw 'The SDK checkout is missing.'}
 if(Test-Path -LiteralPath $sdk){throw 'The SDK destination already exists and is not a Git checkout.'}
 New-Item -ItemType Directory -Path $sdk | Out-Null
 & $git -C $sdk init; if($LASTEXITCODE){throw 'Could not initialize SDK checkout'}
 & $git -C $sdk config core.autocrlf false
 & $git -C $sdk remote add origin https://github.com/rexglue/rexglue-sdk.git
 & $git -C $sdk fetch --depth 1 origin $commit; if($LASTEXITCODE){throw 'Could not download the pinned SDK revision'}
 & $git -C $sdk checkout --detach FETCH_HEAD; if($LASTEXITCODE){throw 'Could not select the pinned SDK revision'}
}
$actual=(& $git -C $sdk rev-parse HEAD).Trim()
if($LASTEXITCODE -or $actual -ne $commit){throw 'Use a separate checkout of the pinned SDK revision; existing changes will not be reset.'}
foreach($patch in $patches){
 $savedErrorPreference=$ErrorActionPreference
 $ErrorActionPreference='Continue'
 try{& $git -C $sdk apply --reverse --check $patch 2>$null;$alreadyApplied=$LASTEXITCODE -eq 0}finally{$ErrorActionPreference=$savedErrorPreference}
 if($alreadyApplied){continue}
 if($VerifyOnly){throw 'The TBRB graphics patch is missing or incomplete.'}
 & $git -C $sdk apply --check $patch; if($LASTEXITCODE){throw 'The SDK patch conflicts with existing changes.'}
 & $git -C $sdk apply $patch; if($LASTEXITCODE){throw 'Could not apply the TBRB graphics patch'}
}
if($VerifyOnly){Write-Output ('Verified SDK '+$commit+' with both TBRB graphics patches');exit 0}
$deps=@('cli11','libmspack','FFmpeg','tomlplusplus','simde','xxHash','spdlog','fmt','utfcpp','imgui','sdl3','o1heap','inja') | ForEach-Object {'thirdparty/'+$_}
& $git -C $sdk submodule update --init --depth 1 --jobs $Jobs -- @deps
if($LASTEXITCODE){throw 'Could not initialize the pinned SDK dependencies'}
# Git for Windows without symlink privileges checks out these links as text.
$module=(Resolve-Path (Join-Path $sdk 'thirdparty/libmspack')).Path
foreach($file in Get-ChildItem -LiteralPath (Join-Path $module 'cabextract/mspack') -File){
 if($file.Length -ge 200){continue}
 $relative=[IO.File]::ReadAllText($file.FullName).Trim()
 if($relative -like '../../libmspack/mspack/*'){
  $target=[IO.Path]::GetFullPath((Join-Path $file.DirectoryName $relative))
  if(-not $target.StartsWith($module+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected dependency link target'}
  Copy-Item -LiteralPath $target -Destination $file.FullName -Force
 }
}
. (Join-Path $PSScriptRoot 'scripts/DevTools.ps1')
$tools=Get-TbrbDevTools -VsInstallDirectory $VsInstallDirectory
$cmake=$tools.CMake
$ninja=$tools.Ninja
& $cmake -S $sdk -B "$sdk/out/build/tbrb" -G Ninja "-DCMAKE_MAKE_PROGRAM=$ninja" "-DCMAKE_C_COMPILER=$($tools.Clang)" "-DCMAKE_CXX_COMPILER=$($tools.ClangPlusPlus)" -DCMAKE_C_COMPILER_TARGET=x86_64-pc-windows-msvc -DCMAKE_CXX_COMPILER_TARGET=x86_64-pc-windows-msvc -DCMAKE_BUILD_TYPE=Release '-DCMAKE_C_FLAGS=-march=x86-64-v2' '-DCMAKE_CXX_FLAGS=-march=x86-64-v2' "-DCMAKE_INSTALL_PREFIX=$install" -DREXGLUE_USE_VULKAN=OFF -DREXGLUE_ENABLE_TRACY=OFF -DREXGLUE_ENABLE_PERF_COUNTERS=OFF -DREXGLUE_BUILD_TESTS=OFF -DREXGLUE_REGISTER_PACKAGE=OFF
if($LASTEXITCODE){exit $LASTEXITCODE}
& $cmake --build "$sdk/out/build/tbrb" --parallel $Jobs
if($LASTEXITCODE){exit $LASTEXITCODE}
& $cmake --install "$sdk/out/build/tbrb"
exit $LASTEXITCODE

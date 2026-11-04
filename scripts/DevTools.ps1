# Shared Windows tool discovery. Does not install tools or change machine settings.
function Get-TbrbDevTools {
 param([string]$VsInstallDirectory)
 if(-not $VsInstallDirectory -and $env:VSINSTALLDIR){$VsInstallDirectory=$env:VSINSTALLDIR}
 if(-not $VsInstallDirectory){
  $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
  if(-not (Test-Path -LiteralPath $vswhere)){throw 'Install Visual Studio 2022 Build Tools with Desktop development with C++, LLVM/Clang and CMake tools, or pass -VsInstallDirectory.'}
  $VsInstallDirectory=@(& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath) | Select-Object -First 1
 }
 if(-not $VsInstallDirectory){throw 'No Visual Studio C++ installation was found. Pass -VsInstallDirectory explicitly.'}
 $vs=[IO.Path]::GetFullPath($VsInstallDirectory)
 $devcmd=Join-Path $vs 'Common7/Tools/VsDevCmd.bat'
 if(-not (Test-Path -LiteralPath $devcmd)){throw ('VsDevCmd.bat not found in '+$vs)}
 $lines=& $env:ComSpec /d /s /c ('""{0}" -no_logo -arch=x64 -host_arch=x64 >nul && set"' -f $devcmd)
 if($LASTEXITCODE){throw 'Could not initialize the Visual Studio x64 build environment.'}
 foreach($line in $lines){if($line -match '^([^=]+)=(.*)$'){[Environment]::SetEnvironmentVariable($matches[1],$matches[2],'Process')}}
 function Resolve-TbrbTool([string]$command,[string]$fallback){
  $found=Get-Command $command -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
  if($found){return $found.Source}
  if(Test-Path -LiteralPath $fallback){return $fallback}
  throw ('Missing '+$command+'. Install the Visual Studio LLVM/Clang and CMake tools components, or add the tool to PATH.')
 }
 $llvm=Join-Path $vs 'VC/Tools/Llvm/bin'
 $cmake=Resolve-TbrbTool 'cmake.exe' (Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe')
 $ninja=Resolve-TbrbTool 'ninja.exe' (Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/Ninja/ninja.exe')
 $clang=Resolve-TbrbTool 'clang.exe' (Join-Path $llvm 'clang.exe')
 $clangpp=Resolve-TbrbTool 'clang++.exe' (Join-Path $llvm 'clang++.exe')
 $clangcl=Resolve-TbrbTool 'clang-cl.exe' (Join-Path $llvm 'clang-cl.exe')
 $env:Path=(Split-Path $clangcl)+';'+(Split-Path $ninja)+';'+$env:Path
 [pscustomobject]@{Vs=$vs;CMake=$cmake;Ninja=$ninja;Clang=$clang;ClangPlusPlus=$clangpp;ClangCl=$clangcl}
}

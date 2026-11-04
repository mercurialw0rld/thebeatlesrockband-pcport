param([string]$OutputDirectory,[string]$ArchivePath)
$ErrorActionPreference='Stop'
if(-not $OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot 'out/BeatlesRockBand-PC'}
if(-not $ArchivePath){$ArchivePath=Join-Path $PSScriptRoot 'out/BeatlesRockBand-PC-v0.7.2-preview-portable.zip'}
New-Item -ItemType Directory -Path (Split-Path ([IO.Path]::GetFullPath($ArchivePath))) -Force | Out-Null
$packageRoot=(Resolve-Path -LiteralPath $OutputDirectory).Path
$files=@('BeatlesRockBandLauncher.exe','launcher.ico','game-files.json','LEEME.md')
$runtime=@('beatlesrockband-guitar.exe','rexruntime.dll','rexgpu-xenos.dll','beatles-input.dll','beatles-content.exe','beatlesrockband.toml',
 'concrt140.dll','msvcp140.dll','msvcp140_1.dll','msvcp140_2.dll','msvcp140_atomic_wait.dll',
 'msvcp140_codecvt_ids.dll','vccorlib140.dll','vcruntime140.dll','vcruntime140_1.dll','vcruntime140_threads.dll')
$files += $runtime | ForEach-Object {'runtime/'+$_}
$files += @('licenses/ReXGlue-LICENSE.txt','licenses/SDL3-LICENSE.txt','licenses/DMSerifDisplay-OFL.txt')
foreach($name in $files){if(-not (Test-Path -LiteralPath (Join-Path $packageRoot $name) -PathType Leaf)){throw ('Missing application file: '+$name)}}
$config=Get-Content -LiteralPath (Join-Path $packageRoot 'runtime/beatlesrockband.toml') -Raw -Encoding UTF8
if($config -match '(?m)^\s*(game_data_root|user_data_root|cache_root|log_file)\s*='){throw 'Portable configuration contains local data paths. Rebuild the launcher package first.'}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archiveFile=[IO.Path]::GetFullPath($ArchivePath)
$temporary=$archiveFile+'.tmp'
try {
 $stream=[IO.File]::Create($temporary)
 $zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
 try {
  foreach($name in $files){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $packageRoot $name),('BeatlesRockBand-PC/'+$name),[IO.Compression.CompressionLevel]::Optimal) | Out-Null}
 } finally {$zip.Dispose();$stream.Dispose()}
 if([IO.File]::Exists($archiveFile)){[IO.File]::Replace($temporary,$archiveFile,[NullString]::Value)}else{[IO.File]::Move($temporary,$archiveFile)}
} catch {if([IO.File]::Exists($temporary)){[IO.File]::Delete($temporary)};throw}
$read=[IO.Compression.ZipFile]::OpenRead($archiveFile)
try {
 if($read.Entries.Count -ne $files.Count){throw 'ZIP file count mismatch.'}
 foreach($entry in $read.Entries){
  if($entry.FullName -match '(?i)(\.(xex|ark|hdr|zbm|pdb)$|/(userdata|cache|logs)/|launcher-settings\.json$)'){throw ('Unexpected file in application package: '+$entry.FullName)}
 }
} finally {$read.Dispose()}
Get-FileHash -LiteralPath $archiveFile -Algorithm SHA256 | Format-List Path,Hash

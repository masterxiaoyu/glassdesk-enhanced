[CmdletBinding()]
param(
  [string]$Version = '1.6.0-preview.10',
  [string]$FileVersion = '1.6.0.0',
  [Parameter(Mandatory = $true)][string]$DotnetPath,
  [Parameter(Mandatory = $true)][string]$InnoCompilerPath
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$release = Join-Path $repo ('artifacts/public-release/' + $Version)
$stage = Join-Path $release ('payload-' + [guid]::NewGuid().ToString('N'))
$base = 'GlassDesk-' + $Version + '-win-x64'
New-Item -ItemType Directory -Path $stage -Force | Out-Null

& $DotnetPath publish (Join-Path $repo 'src/GlassDesk.App/GlassDesk.App.csproj') `
  -c Release -r win-x64 --self-contained true --no-restore -m:1 `
  -p:UseSharedCompilation=false -p:IncludeSourceRevisionInInformationalVersion=false `
  "-p:Version=$Version" "-p:FileVersion=$FileVersion" `
  "-p:InformationalVersion=$Version" -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$documents = @{
  'LICENSE' = 'LICENSE.txt'
  'NOTICE' = 'NOTICE.txt'
  'packaging/USER-GUIDE.md' = 'README.md'
  'LOCAL-MODIFICATIONS.md' = 'LOCAL-MODIFICATIONS.md'
  'SOURCE-PROVENANCE.json' = 'SOURCE-PROVENANCE.json'
  'docs/LICENSING.md' = 'LICENSING.md'
  'docs/INSTALLATION.md' = 'INSTALLATION.md'
  'packaging/INSTALLER-NOTICE.txt' = 'INSTALLER-NOTICE.txt'
  "packaging/RELEASE-NOTES-$Version.md" = 'RELEASE-NOTES.md'
}
foreach ($entry in $documents.GetEnumerator()) {
  Copy-Item -LiteralPath (Join-Path $repo $entry.Key) `
    -Destination (Join-Path $stage $entry.Value)
}
Copy-Item -LiteralPath (Join-Path $repo 'docs/third-party') `
  -Destination (Join-Path $stage 'third-party') -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'docs/licensing') `
  -Destination (Join-Path $stage 'licensing') -Recurse

# Only a fresh publish is packaged; private preview components are never copied.
$forbidden = Get-ChildItem -LiteralPath $stage -Recurse -Force | Where-Object {
  $_.Name -eq 'Data' -or $_.Name -match '\.(pfx|p12|cer|msix|lease)$'
}
if ($forbidden) { throw 'Private data or preview components in payload.' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo(
  (Join-Path $stage 'GlassDesk.exe'))
if ($info.FileVersion -ne $FileVersion -or $info.ProductVersion -ne $Version) {
  throw 'Executable version mismatch.'
}

Add-Type -AssemblyName System.IO.Compression
$zip = Join-Path $release ($base + '-portable.zip')
$stream = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
$archive = [IO.Compression.ZipArchive]::new(
  $stream, [IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File) {
    $name = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
      $archive, $file.FullName, $name,
      [IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
}
finally { $archive.Dispose(); $stream.Dispose() }

$previousStage = $env:GLASSDESK_PACKAGE_STAGE
try {
  $env:GLASSDESK_PACKAGE_STAGE = $stage
  & $InnoCompilerPath /Q '/DAppIdValue=GlassDesk.Independent' `
    "/DProductVersion=$Version" "/DFileVersion=$FileVersion" `
    "/DPackageBaseName=$base" ("/O" + $release) `
    (Join-Path $repo 'packaging/GlassDesk.iss')
  if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
finally { $env:GLASSDESK_PACKAGE_STAGE = $previousStage }

$setup = Join-Path $release ($base + '-Setup.exe')
$hashes = foreach ($file in @($zip, $setup)) {
  $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
  $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($file)
}
$hashes | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') `
  -Encoding ascii
@{
  version = $Version
  fileVersion = $FileVersion
  appId = 'GlassDesk.Independent'
  payload = $stage
  notificationComponentIncluded = $false
  personalDataIncluded = $false
} | ConvertTo-Json | Set-Content (Join-Path $release 'local-build.json')
Write-Output $release

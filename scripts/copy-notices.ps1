param([Parameter(Mandatory = $true)][string]$LibraryDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $LibraryDirectory 'licenses'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses/HidLibrary.txt') -Destination $destination
Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses/NAudio.txt') -Destination $destination
Add-Type -AssemblyName System.IO.Compression.FileSystem
[xml]$packages = Get-Content -LiteralPath (Join-Path $projectRoot 'StoKOdnomuControl/packages.config')
$notices = [Collections.Generic.List[string]]::new()
$notices.Add('Third-party components bundled with StoKOdnomuControl')
$notices.Add('JetBrains Mono: see Fonts/OFL.txt. Package notices and licenses follow.')
foreach ($package in $packages.packages.package) {
    $packageDirectory = Join-Path $projectRoot ('packages/' + $package.id + '.' + $package.version)
    $archivePath = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nupkg' | Select-Object -First 1
    if (-not $archivePath) { throw "Package archive missing: $packageDirectory" }
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath.FullName)
    try {
        $spec = $archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec') } | Select-Object -First 1
        $reader = [IO.StreamReader]::new($spec.Open())
        try { [xml]$metadata = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $entry = $metadata.SelectSingleNode("//*[local-name()='metadata']")
        $notices.Add('')
        $notices.Add($package.id + ' ' + $package.version)
        foreach ($field in @('authors', 'copyright', 'license', 'licenseUrl', 'projectUrl')) {
            $value = $entry.SelectSingleNode("*[local-name()='$field']")
            if ($value) { $notices.Add($field + ': ' + $value.InnerText) }
        }
        foreach ($file in $archive.Entries) {
            $name = [IO.Path]::GetFileName($file.FullName)
            if ($name -notmatch '^(LICENSE|LICENCE|THIRD.PARTY.NOTICES)') { continue }
            $target = Join-Path $destination ($package.id + '-' + $package.version + '-' + $name)
            $inputStream = $file.Open()
            $outputStream = [IO.File]::Create($target)
            try { $inputStream.CopyTo($outputStream) } finally { $inputStream.Dispose(); $outputStream.Dispose() }
        }
    } finally { $archive.Dispose() }
}
[IO.File]::WriteAllLines((Join-Path $LibraryDirectory 'THIRD_PARTY_NOTICES.txt'), $notices, [Text.UTF8Encoding]::new($false))

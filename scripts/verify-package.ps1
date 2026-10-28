param([Parameter(Mandatory = $true)][string]$AppDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$appPath = (Resolve-Path -LiteralPath $AppDirectory).Path
$msbuild = & (Join-Path $PSScriptRoot 'find-msbuild.ps1')
& $msbuild (Join-Path $projectRoot 'Tests/RegressionTests.vbproj') /t:Build /p:Configuration=Release /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "Test runner build failed with code $LASTEXITCODE." }
# Проверяем исходный комплект до добавления временного тестового EXE.
foreach ($entry in Get-ChildItem -LiteralPath $appPath -Force) {
    if ($entry.Name -notin @('StoKOdnomuControl.exe', 'StoKOdnomuControl.exe.config', 'lib')) { throw "Unexpected release root entry: $($entry.Name)" }
}
$listed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$manifest = Join-Path $appPath 'lib/PACKAGE_MANIFEST.txt'
foreach ($line in Get-Content -LiteralPath $manifest -Encoding UTF8) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw "Invalid manifest entry: $line" }
    $expected = $Matches[1]
    $relative = $Matches[2]
    if (-not $listed.Add($relative.Replace('\', '/'))) { throw "Duplicate manifest entry: $relative" }
    $path = [IO.Path]::GetFullPath((Join-Path $appPath $relative))
    if (-not $path.StartsWith($appPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes package.' }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $expected) { throw "Manifest mismatch: $relative" }
}
foreach ($file in Get-ChildItem -LiteralPath $appPath -Recurse -File) {
    $relative = $file.FullName.Substring($appPath.Length + 1).Replace('\', '/')
    if ($relative -ne 'lib/PACKAGE_MANIFEST.txt' -and -not $listed.Contains($relative)) { throw "Unlisted package file: $relative" }
}
# Копия комплекта живёт отдельно от исходников и рабочего bin; проверочный EXE
# добавляется только в эту временную копию и никогда не попадает в готовый архив.
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('StoKOdnomu-package-' + [Guid]::NewGuid().ToString('N'))
$copy = Join-Path $temporary 'relocated-app'
$reports = Join-Path $temporary 'reports'
$working = Join-Path $temporary 'empty-working-directory'
New-Item -ItemType Directory -Path $copy, $reports, $working | Out-Null
Copy-Item -Path (Join-Path $appPath '*') -Destination $copy -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tests/bin/Release/RegressionTests.exe') -Destination $copy
Copy-Item -LiteralPath (Join-Path $appPath 'StoKOdnomuControl.exe.config') -Destination (Join-Path $copy 'RegressionTests.exe.config')
$report = Join-Path $reports 'package-checks.txt'
$process = Start-Process -FilePath (Join-Path $copy 'RegressionTests.exe') -ArgumentList @('--package-smoke', ('"' + $report + '"')) -WorkingDirectory $working -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'Package check exceeded 120 seconds.' }
if ($process.ExitCode -ne 0) { throw "Package check failed with code $($process.ExitCode); see $reports" }
Get-Content -LiteralPath $report
Write-Output "Снимки и отчёт: $reports"

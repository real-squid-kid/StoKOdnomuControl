param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$Package
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$msbuildPath = & (Join-Path $PSScriptRoot 'find-msbuild.ps1')
$buildArguments = @((Join-Path $projectRoot 'StoKOdnomuControl/StoKOdnomuControl.vbproj'), '/t:Build', "/p:Configuration=$Configuration", '/nologo', '/verbosity:minimal')
if ($Package) {
    # Новый каталог исключает старые DLL из комплекта; существующие файлы не удаляем.
    $packageRoot = Join-Path $projectRoot ('artifacts/release-' + [TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow, 'Russian Standard Time').ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $buildArguments += "/p:OutputPath=$packageRoot/app/"
    $buildArguments += "/p:BaseIntermediateOutputPath=$packageRoot/obj/"
    $buildArguments += "/p:IntermediateOutputPath=$packageRoot/obj/$Configuration/"
}
& $msbuildPath @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с кодом $LASTEXITCODE." }
if ($Package) {
    $appPath = Join-Path $packageRoot 'app'
    foreach ($document in @('README.MD', 'OPERATOR_GUIDE.MD')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination (Join-Path $appPath $document)
    }
    & (Join-Path $PSScriptRoot 'copy-notices.ps1') -AppDirectory $appPath
    # Убираем только отладочные файлы из НОВОГО комплекта, не из рабочего bin.
    foreach ($generated in Get-ChildItem -LiteralPath $appPath -File | Where-Object { $_.Extension -eq '.pdb' -or ($_.Extension -eq '.xml' -and ($_.BaseName -eq 'StoKOdnomuControl' -or (Test-Path -LiteralPath (Join-Path $appPath ($_.BaseName + '.dll'))))) }) {
        Remove-Item -LiteralPath $generated.FullName
    }
    $manifest = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $appPath -Recurse -File | Sort-Object FullName) {
        $relative = $file.FullName.Substring($appPath.Length + 1).Replace('\', '/')
        $manifest.Add((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative)
    }
    [IO.File]::WriteAllLines((Join-Path $appPath 'PACKAGE_MANIFEST.txt'), $manifest, [Text.UTF8Encoding]::new($false))
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = Join-Path $packageRoot 'StoKOdnomuControl.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($appPath, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
    Write-Output "Комплект запуска: $appPath"
    Write-Output "Архив: $zip"
}

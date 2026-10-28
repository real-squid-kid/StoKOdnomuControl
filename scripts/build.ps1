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
    $libPath = Join-Path $appPath 'lib'
    $docsPath = Join-Path $libPath 'docs'
    New-Item -ItemType Directory -Path $docsPath -Force | Out-Null
    foreach ($document in @('README.MD', 'OPERATOR_GUIDE.MD', 'ARCHITECTURE.MD', 'VERIFY.md', 'LICENSE')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination (Join-Path $docsPath $document)
    }
    & (Join-Path $PSScriptRoot 'copy-notices.ps1') -LibraryDirectory $libPath
    # Убираем только отладочные файлы из НОВОГО комплекта, не из рабочего bin.
    foreach ($generated in Get-ChildItem -LiteralPath $appPath -Recurse -File) {
        $symbols = $generated.Extension -eq '.pdb'
        $localDll = Join-Path $generated.DirectoryName ($generated.BaseName + '.dll')
        $libraryDll = Join-Path $libPath ($generated.BaseName + '.dll')
        $assemblyDocumentation = $generated.Extension -eq '.xml' -and (
            $generated.BaseName -eq 'StoKOdnomuControl' -or
            (Test-Path -LiteralPath $localDll) -or (Test-Path -LiteralPath $libraryDll))
        if ($symbols -or $assemblyDocumentation) { Remove-Item -LiteralPath $generated.FullName }
    }
    foreach ($entry in Get-ChildItem -LiteralPath $appPath -Force) {
        if ($entry.Name -notin @('StoKOdnomuControl.exe', 'StoKOdnomuControl.exe.config', 'lib')) {
            throw "Unexpected release root entry: $($entry.Name)"
        }
    }
    $manifest = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $appPath -Recurse -File | Sort-Object FullName) {
        $relative = $file.FullName.Substring($appPath.Length + 1).Replace('\', '/')
        $manifest.Add((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative)
    }
    [IO.File]::WriteAllLines((Join-Path $libPath 'PACKAGE_MANIFEST.txt'), $manifest, [Text.UTF8Encoding]::new($false))
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = Join-Path $packageRoot 'StoKOdnomuControl.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($appPath, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
    Write-Output "Комплект запуска: $appPath"
    Write-Output "Архив: $zip"
}

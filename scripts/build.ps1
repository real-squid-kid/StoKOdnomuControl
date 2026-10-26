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
    $packageRoot = Join-Path $projectRoot ('artifacts/release-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $buildArguments += "/p:OutputPath=$packageRoot/app/"
    $buildArguments += "/p:BaseIntermediateOutputPath=$packageRoot/obj/"
    $buildArguments += "/p:IntermediateOutputPath=$packageRoot/obj/$Configuration/"
}
& $msbuildPath @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с кодом $LASTEXITCODE." }
if ($Package) {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'OPERATOR_GUIDE.MD') -Destination (Join-Path $packageRoot 'app/OPERATOR_GUIDE.MD')
    Write-Output "Комплект запуска: $packageRoot/app"
}

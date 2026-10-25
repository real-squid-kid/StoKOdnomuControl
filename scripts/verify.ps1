param([switch]$Ui)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$msbuildPath = & (Join-Path $PSScriptRoot 'find-msbuild.ps1')
& $msbuildPath (Join-Path $projectRoot 'StoKOdnomuControl.sln') /t:Build /p:Configuration=Release /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с кодом $LASTEXITCODE." }
$testArguments = @()
if ($Ui) { $testArguments += '--ui' }
& (Join-Path $projectRoot 'Tests/bin/Release/RegressionTests.exe') @testArguments
if ($LASTEXITCODE -ne 0) { throw "Проверки завершились с кодом $LASTEXITCODE." }

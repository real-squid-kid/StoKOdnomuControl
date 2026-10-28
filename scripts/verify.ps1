param([switch]$Ui, [switch]$Stability)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
# Ошибка компиляции останавливает запуск старого тестового EXE.
$msbuildPath = & (Join-Path $PSScriptRoot 'find-msbuild.ps1')
& $msbuildPath (Join-Path $projectRoot 'StoKOdnomuControl.sln') /t:Build /p:Configuration=Release /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с кодом $LASTEXITCODE." }
$testArguments = @()
# UI-проверки включаются явно: они создают формы и сохраняют снимки в тестовый каталог.
if ($Ui) { $testArguments += '--ui' }
if ($Stability) { $testArguments += '--stability' }
& (Join-Path $projectRoot 'Tests/bin/Release/RegressionTests.exe') @testArguments
if ($LASTEXITCODE -ne 0) { throw "Проверки завершились с кодом $LASTEXITCODE." }

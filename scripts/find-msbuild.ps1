$ErrorActionPreference = 'Stop'
$msbuildCommand = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
if ($msbuildCommand) { return $msbuildCommand.Source }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (Test-Path -LiteralPath $vswhere) {
    $msbuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if ($msbuildPath) { return $msbuildPath }
}
throw 'Установите Visual Studio 2022 / Build Tools с MSBuild и .NET Framework 4.8 Developer Pack.'

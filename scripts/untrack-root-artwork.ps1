param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent

# По умолчанию только просмотр. -Apply меняет индекс, исходные файлы остаются.
# Обрабатываются лишь PNG/XCF непосредственно в корне, без профилей и screenshots.
$tracked = @(& git -C $projectRoot -c core.quotepath=false ls-files -- '*.png' '*.PNG' '*.xcf' '*.XCF')
if ($LASTEXITCODE -ne 0) { throw 'Не удалось прочитать список файлов Git.' }
$artwork = @($tracked | Where-Object { $_ -notmatch '[/\\]' -and [IO.Path]::GetExtension($_) -match '^\.(png|xcf)$' })
if ($artwork.Count -eq 0) {
    Write-Output 'Корневых PNG/XCF в индексе нет.'
    return
}
Write-Output 'Файлы для удаления только из индекса:'
$artwork | Write-Output
if (-not $Apply) {
    Write-Output 'Для применения запустите ./scripts/untrack-root-artwork.ps1 -Apply'
    return
}
foreach ($file in $artwork) {
    & git -C $projectRoot rm --cached -- $file
    if ($LASTEXITCODE -ne 0) { throw "Не удалось убрать из индекса: $file" }
}
Write-Output 'Локальные файлы сохранены. Коммит, тег и синхронизация не выполнялись.'

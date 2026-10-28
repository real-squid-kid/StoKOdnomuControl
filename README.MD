# Сто к одному

Табло и пульт оператора для проведения игры «Сто к одному»: основная и большая игра, счёт, промахи, заставки, таймер, кнопки Buzz!, оформления и редактор баз. Windows Forms, VB.NET, .NET Framework 4.8.

## Запуск из release

1. Скачайте ZIP из Releases и распакуйте его целиком в новую папку.
2. Откройте app и запустите **StoKOdnomuControl.exe**.
3. В настройках выберите зрительский экран, оформление и звук. Загрузите базу из lib/bases и начните раунд.

Нужны Windows и .NET Framework 4.8. Visual Studio и установка шрифтов для запуска не требуются. Рекомендуемый масштаб Windows — 100%; другой масштаб вызывает предупреждение с инструкцией по настройке.

```text
app/
  StoKOdnomuControl.exe
  StoKOdnomuControl.exe.config
  lib/                         DLL, шрифты, оформления и базы
    docs/                      памятка, документация и LICENSE
```

Переносите всю папку app. Не удаляйте .config и lib. Рабочий матч и выбранное оформление хранятся отдельно: `%LOCALAPPDATA%/StoKOdnomuControl`. Перед обновлением сохраните копию этой папки при закрытом приложении.

Управление игрой, настройки и восстановление описаны в [памятке оператора](OPERATOR_GUIDE.MD); в release она находится в lib/docs.

## Самостоятельная сборка

Нужны Visual Studio 2022 с компонентом «Разработка классических приложений .NET» и .NET Framework 4.8 Developer Pack. Откройте StoKOdnomuControl.sln, восстановите пакеты NuGet и выберите StoKOdnomuControl стартовым проектом.

Из PowerShell в корне исходников:

```powershell
./scripts/build.ps1
./scripts/build.ps1 -Package
```

Первая команда собирает Release; `-Configuration Debug` выбирает Debug. `-Package` создаёт новую папку artifacts/release-… с app и ZIP. После восстановления NuGet сборка работает без сети.

[Проверки и упаковка](VERIFY.md) · [Архитектура и форматы данных](ARCHITECTURE.MD)

## Лицензия

[MIT](LICENSE) — Copyright (c) 2026 Vasily Makrov. Лицензии сторонних библиотек и шрифтов включены в комплект отдельно.

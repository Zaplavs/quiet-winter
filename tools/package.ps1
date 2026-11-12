$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $projectRoot 'artifacts\release'
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\QuietWinter.exe') -Destination (Join-Path $releaseDir 'QuietWinter.exe')
Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Destination (Join-Path $releaseDir 'CHANGELOG.md')
@'
ТИХАЯ ЗИМА / QUIET WINTER 1.1.0

Windows 10/11 x64. Запустите QuietWinter.exe двойным щелчком.
Установка, права администратора и интернет не нужны.

Настройки: двойной щелчок по снежинке в трее рядом с часами.
Ctrl+Alt+S — пауза. Ctrl+Alt+B — снежный вихрь.
Shift + движение курсора — смахнуть снег. Ctrl+Alt+Q — выключить.
Закрытие окна настроек оставляет снег включённым.

Исходники, видео и обновления: https://github.com/Zaplavs/quiet-winter
Telegram: https://t.me/+ay37cKnFWtg3MDJi
Поддержать: https://www.donationalerts.com/r/ruszaphack
'@ | Set-Content -LiteralPath (Join-Path $releaseDir 'README.txt') -Encoding utf8
$archive = Join-Path $projectRoot 'QuietWinter-windows-x64.zip'
$files = @((Join-Path $releaseDir 'QuietWinter.exe'), (Join-Path $releaseDir 'README.txt'), (Join-Path $releaseDir 'CHANGELOG.md'))
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
$hashFiles = @((Join-Path $releaseDir 'QuietWinter.exe'), $archive)
$hashLines = foreach ($file in $hashFiles) { $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256; "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($file))" }
$hashLines | Set-Content -LiteralPath (Join-Path $releaseDir 'SHA256SUMS.txt') -Encoding ascii
Write-Output $archive

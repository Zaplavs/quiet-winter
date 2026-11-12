$ErrorActionPreference = 'Stop'
$sourceExe = Join-Path $PSScriptRoot 'dist\QuietWinter.exe'
if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) { throw 'Сначала запустите build.ps1 или распакуйте полный архив приложения.' }
$desktopPath = [Environment]::GetFolderPath('Desktop')
if (-not [System.IO.Path]::IsPathRooted($desktopPath)) { throw 'Не удалось определить рабочий стол.' }
$targetExe = Join-Path $desktopPath 'Тихая зима.exe'
if (Test-Path -LiteralPath $targetExe) {
    $existing = Get-Item -LiteralPath $targetExe
    if ($existing.VersionInfo.ProductName -ne 'Quiet Winter') { throw "Файл $targetExe уже существует и не принадлежит этому приложению. Он не изменён." }
    $stop = Start-Process -FilePath $targetExe -ArgumentList '--stop' -WindowStyle Hidden -Wait -PassThru
    Start-Sleep -Milliseconds 500
}
Copy-Item -LiteralPath $sourceExe -Destination $targetExe
Write-Output "Готово: $targetExe"
Start-Process -FilePath $targetExe -WindowStyle Hidden

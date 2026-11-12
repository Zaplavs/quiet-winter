$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Для сборки нужен .NET Framework 4.x (входит в Windows 10/11).' }
$outputDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/utf8output',
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Runtime.Serialization.dll',
    ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')),
    ('/out:' + (Join-Path $outputDir 'QuietWinter.exe')))
$iconPath = Join-Path $projectRoot 'assets\winter.ico'
if (Test-Path -LiteralPath $iconPath) { $arguments += '/win32icon:' + $iconPath }
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Компиляция не удалась.' }
Write-Output (Join-Path $outputDir 'QuietWinter.exe')

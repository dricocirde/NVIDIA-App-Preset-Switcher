param(
    [ValidateSet('winexe','exe')]
    [string]$Target = 'winexe'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src\NvidiaPresetSwitcher'
$output = Join-Path $root 'outputs\NvidiaAppPresetSwitcher.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $compiler)) { throw "C# compiler not found: $compiler" }
New-Item -ItemType Directory -Force (Split-Path -Parent $output) | Out-Null

$files = Get-ChildItem $source -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler /nologo /optimize+ /platform:x64 "/target:$Target" "/out:$output" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll $files
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
Copy-Item (Join-Path $source 'NvidiaPresetSwitcher.exe.config') ($output + '.config') -Force
Write-Host "Built: $output"

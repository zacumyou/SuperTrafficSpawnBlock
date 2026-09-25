$ErrorActionPreference = 'Stop'
if (Get-Process Cities2 -ErrorAction SilentlyContinue) { throw 'Close Cities: Skylines II before installing this mod.' }
$spawnSource = Join-Path $PSScriptRoot 'local\SuperTrafficSpawnBlock'
$spawnMods = [IO.Path]::GetFullPath((Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II\Mods'))
$spawnTarget = [IO.Path]::GetFullPath((Join-Path $spawnMods 'SuperTrafficSpawnBlock'))
if ((Split-Path $spawnTarget -Parent) -ne $spawnMods -or (Split-Path $spawnTarget -Leaf) -ne 'SuperTrafficSpawnBlock') { throw 'Unexpected install target.' }
$spawnFiles = @('SuperTrafficSpawnBlock.dll', 'SuperTrafficSpawnBlock.pdb', 'SuperTrafficSpawnBlock_win_x86_64.dll', 'SuperTrafficSpawnBlock_win_x86_64.pdb', 'SuperTrafficSpawnBlock.mjs')
foreach ($name in $spawnFiles) { if (!(Test-Path -LiteralPath (Join-Path $spawnSource $name))) { throw "Build output missing: $name" } }
foreach ($factor in @(1,2,5,10,20,50)) { if (!(Test-Path -LiteralPath (Join-Path $spawnSource "icons\${factor}x.svg"))) { throw "Missing icon: $factor" } }
if (!(Test-Path -LiteralPath (Join-Path $spawnSource 'icons\sink.svg'))) { throw 'Missing sink icon' }
if (!(Test-Path -LiteralPath (Join-Path $spawnSource 'icons\source.svg'))) { throw 'Missing source icon' }
$spawnBackup = $null
if (Test-Path -LiteralPath $spawnTarget) {
    $spawnBackup = Join-Path $PSScriptRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $spawnBackup -Force | Out-Null
    Copy-Item -LiteralPath $spawnTarget -Destination $spawnBackup -Recurse
}
New-Item -ItemType Directory -Path $spawnTarget -Force | Out-Null
$spawnManifest = foreach ($name in $spawnFiles) {
    $source = Join-Path $spawnSource $name
    $target = Join-Path $spawnTarget $name
    Copy-Item -LiteralPath $source -Destination $target -Force
    $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $targetHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($sourceHash -ne $targetHash) { throw "Installed hash mismatch: $name" }
    [pscustomobject]@{ File = $name; SHA256 = $targetHash; Bytes = (Get-Item -LiteralPath $target).Length }
}
Copy-Item -LiteralPath (Join-Path $spawnSource 'icons') -Destination $spawnTarget -Recurse -Force
Copy-Item -LiteralPath (Join-Path $spawnSource 'lang') -Destination $spawnTarget -Recurse -Force
Copy-Item -LiteralPath (Join-Path $spawnSource 'assets') -Destination $spawnTarget -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TRANSLATING.md') -Destination $spawnTarget -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'readme.txt') -Destination (Join-Path $spawnTarget 'readme.txt') -Force
[pscustomobject]@{
    Version = '0.4.0'; InstalledAt = (Get-Date -Format o); Target = $spawnTarget; Backup = $spawnBackup
    GameRunning = $false; InGameVerified = $false; Files = $spawnManifest
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $PSScriptRoot 'research\install.json') -Encoding utf8
Write-Output "Installed and verified: $spawnTarget"
$spawnManifest | Format-Table -AutoSize














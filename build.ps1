$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Path research -Force | Out-Null
    $spawnNode = (Get-Command node -ErrorAction Stop).Source
    & $spawnNode ui\node_modules\webpack-cli\bin\cli.js --config .\ui\webpack.config.cjs *> .\research\ui-build.log
    if ($LASTEXITCODE -ne 0) { Get-Content .\research\ui-build.log; throw 'UI build failed.' }
    dotnet build .\src\SuperTrafficSpawnBlock.csproj -c Release --nologo *> .\research\build.log
    if ($LASTEXITCODE -ne 0) { Get-Content .\research\build.log -Tail 30; throw 'Build failed.' }
    dotnet run --project .\tests\DispatchTests.csproj -c Release *> .\research\tests.log
    if ($LASTEXITCODE -ne 0) { Get-Content .\research\tests.log; throw 'Dispatch tests failed.' }
    Get-Content .\research\build.log -Tail 6
    Get-Content .\research\tests.log
    & $spawnNode .\tests\ui-test.cjs
    if ($LASTEXITCODE -ne 0) { throw 'Destination UI test failed.' }
    & $spawnNode .\tests\localization-test.cjs
    if ($LASTEXITCODE -ne 0) { throw 'Localization test failed.' }
} finally { Pop-Location }



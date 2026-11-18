[CmdletBinding()]
param(
    [string]$Salida = (Join-Path $PSScriptRoot 'bin/releases'),
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$csproj = Join-Path $PSScriptRoot 'Gineco.csproj'
$version = ([xml](Get-Content -LiteralPath $csproj -Raw)).Project.PropertyGroup.Version
$publicado = Join-Path $PSScriptRoot ('bin/publicado-' + [guid]::NewGuid().ToString('N'))
$salidaAbsoluta = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Salida)
$env:DOTNET_ROOT_X64 = Split-Path (Get-Command $Dotnet).Source

Push-Location (Join-Path $PSScriptRoot '..')
try {
    & $Dotnet tool restore
    if ($LASTEXITCODE) { throw 'No se pudo restaurar Velopack.' }
    & $Dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:TreatWarningsAsErrors=true -p:DebugType=None -p:DebugSymbols=false -o $publicado
    if ($LASTEXITCODE) { throw 'No se pudo publicar Gineco.' }
    $empaquetado = @(
        '--packId', 'HospitalContreras.Gineco', '--packTitle', 'Gineco',
        '--packVersion', $version, '--packDir', $publicado, '--mainExe', 'Gineco.exe',
        '--runtime', 'win-x64', '--outputDir', $salidaAbsoluta
    )
    & $Dotnet tool run vpk pack @empaquetado
    if ($LASTEXITCODE) { throw 'No se pudo crear el instalador.' }
}
finally {
    Pop-Location
}

[CmdletBinding(DefaultParameterSetName = 'Instalar')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Exportar')]
    [ValidateSet('admision', 'consulta')][string]$Funcion,
    [Parameter(Mandatory, ParameterSetName = 'Exportar')][string]$ExportarA
)
$ErrorActionPreference = 'Stop'

function Validar-Vinculo($Vinculo) {
    if ($Vinculo.funcion -cnotin @('admision', 'consulta') -or
        $Vinculo.proyecto -cnotmatch '^[a-z0-9]{20}$' -or
        $Vinculo.clavePublica -cnotmatch '^sb_publishable_[a-zA-Z0-9_-]+$' -or
        $Vinculo.token -cnotmatch '^[a-zA-Z0-9+/]{43}=$') {
        throw 'El archivo de vinculo es invalido o esta incompleto.'
    }
}

if ($PSCmdlet.ParameterSetName -eq 'Exportar') {
    $vinculo = @{
        funcion = $Funcion
        proyecto = [Environment]::GetEnvironmentVariable('GINECO_PROYECTO', 'User')
        clavePublica = [Environment]::GetEnvironmentVariable('GINECO_CLAVE_PUBLICA', 'User')
        token = [Environment]::GetEnvironmentVariable("GINECO_TOKEN_$($Funcion.ToUpperInvariant())", 'User')
    }
    Validar-Vinculo $vinculo
    $destino = Join-Path $ExportarA "Vinculo-Gineco-$Funcion"
    New-Item -Path $destino -ItemType Directory | Out-Null
    $vinculo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destino 'Vinculo-Gineco-estacion.json') -Encoding UTF8
    Copy-Item -LiteralPath $PSCommandPath, (Join-Path $PSScriptRoot 'Vincular-Estacion.cmd') -Destination $destino
    Write-Output "Vinculo preparado para $Funcion."
    return
}

$archivo = Join-Path $PSScriptRoot 'Vinculo-Gineco-estacion.json'
if (-not (Test-Path -LiteralPath $archivo -PathType Leaf)) {
    throw 'Falta el archivo privado del puesto. Extrae la carpeta de vinculo completa.'
}
$vinculo = Get-Content -LiteralPath $archivo -Raw | ConvertFrom-Json
Validar-Vinculo $vinculo
$anterior = [Environment]::GetEnvironmentVariable('GINECO_PROYECTO', 'User')
if ($anterior -and $anterior -cne $vinculo.proyecto) {
    throw 'Esta cuenta de Windows ya esta vinculada a otro proyecto de Ginecologia.'
}
[Environment]::SetEnvironmentVariable('GINECO_PROYECTO', $vinculo.proyecto, 'User')
[Environment]::SetEnvironmentVariable('GINECO_CLAVE_PUBLICA', $vinculo.clavePublica, 'User')
[Environment]::SetEnvironmentVariable("GINECO_TOKEN_$($vinculo.funcion.ToUpperInvariant())", $vinculo.token, 'User')
Write-Output "Equipo vinculado como $($vinculo.funcion). Cierra y vuelve a abrir la aplicacion."

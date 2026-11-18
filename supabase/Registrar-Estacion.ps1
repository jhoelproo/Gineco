[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('admision','consulta')][string]$Funcion,
    [ValidatePattern('^[a-z0-9]{20}$')][string]$Proyecto = 'agnhgwtmczrtuqlsuefx',
    [ValidatePattern('^sb_publishable_[a-zA-Z0-9_-]+$')][string]$ClavePublica = 'sb_publishable_SLBaZpWFq-7RnF3CNYsi7w_dNgcf-pO'
)
$ErrorActionPreference = 'Stop'
$nombre = 'GINECO_TOKEN_' + $Funcion.ToUpperInvariant()
$anterior = [Environment]::GetEnvironmentVariable('GINECO_PROYECTO','User')
if ($anterior -and $anterior -ne $Proyecto) {
    throw 'Esta cuenta de Windows ya tiene configurado otro proyecto de Ginecología.'
}
$token = [Environment]::GetEnvironmentVariable($nombre,'User')
if (-not $token) {
    $bytes = New-Object byte[] 32
    $azar = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $azar.GetBytes($bytes) } finally { $azar.Dispose() }
    $token = [Convert]::ToBase64String($bytes)
    [Environment]::SetEnvironmentVariable($nombre,$token,'User')
}
[Environment]::SetEnvironmentVariable('GINECO_PROYECTO',$Proyecto,'User')
[Environment]::SetEnvironmentVariable('GINECO_CLAVE_PUBLICA',$ClavePublica,'User')
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $huella = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($token))).Replace('-','')
} finally { $sha.Dispose() }
"insert into gineco_privado.estacion(huella,funcion) values (decode('$huella','hex'),'$Funcion') on conflict do nothing;"

# setup-dev-secrets.ps1 - cria os segredos locais de desenvolvimento.
#
# Roda UMA VEZ. Gera uma Jwt:Key nova e define o usuario Owner que o backend
# cria sozinho no boot (DataSeeder). Grava tudo via "dotnet user-secrets set",
# que escreve no lugar exato de onde o "dotnet run" le.
#
# Uso:
#   .\setup-dev-secrets.ps1
#   .\setup-dev-secrets.ps1 -OwnerEmail "eu@filippon.com" -OwnerPassword "MinhaSenha123"
#
# NAO afeta producao: o Railway tem as proprias variaveis de ambiente.

param(
    [string]$OwnerEmail    = 'dev@filippon.local',
    # Sem valor fixo de proposito. Uma senha padrao conhecida vira conta de
    # administrador com senha fraca no primeiro banco em que o backend subir -
    # e se alguem rodar isto com a connection string apontada para producao,
    # essa conta nasce no banco dos clientes. Vazio = sorteamos uma.
    [string]$OwnerPassword = ''
)

$ErrorActionPreference = 'Stop'

function New-SenhaForte {
    $bytes = New-Object byte[] 18
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    # Base64 tem +/= que atrapalham colar em terminal; troca por letras.
    return ([Convert]::ToBase64String($bytes) -replace '\+','A' -replace '/','9' -replace '=','') + '7z'
}

$projeto = Join-Path $PSScriptRoot 'src\Plataforma.Api'

if (-not (Test-Path (Join-Path $projeto 'Plataforma.Api.csproj'))) {
    Write-Host "X Nao achei o projeto em: $projeto" -ForegroundColor Red
    Write-Host '  Rode este script de dentro da pasta backend.' -ForegroundColor Yellow
    exit 1
}

if (-not $OwnerPassword) {
    $OwnerPassword = New-SenhaForte
    Write-Host ''
    Write-Host '  (senha do Owner sorteada - anote abaixo, ela nao e recuperavel)' -ForegroundColor DarkGray
}

if ($OwnerPassword.Length -lt 8) {
    Write-Host 'X A senha do Owner precisa ter ao menos 8 caracteres.' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host '=== Gerando Jwt:Key ===' -ForegroundColor Cyan

# 64 bytes aleatorios em base64. Chave de assinatura HS256 dos tokens de login.
$bytes = New-Object byte[] 64
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$jwtKey = [Convert]::ToBase64String($bytes)

Write-Host "  gerada ($($jwtKey.Length) caracteres)" -ForegroundColor Green

Write-Host ''
Write-Host '=== Gravando no user-secrets ===' -ForegroundColor Cyan

dotnet user-secrets set 'Jwt:Key' $jwtKey --project $projeto | Out-Null
Write-Host '  ok Jwt:Key' -ForegroundColor Green

dotnet user-secrets set 'Seed:Owner:Email' $OwnerEmail --project $projeto | Out-Null
Write-Host "  ok Seed:Owner:Email = $OwnerEmail" -ForegroundColor Green

dotnet user-secrets set 'Seed:Owner:Password' $OwnerPassword --project $projeto | Out-Null
Write-Host "  ok Seed:Owner:Password = $OwnerPassword" -ForegroundColor Green

Write-Host ''
Write-Host '=== Conferindo ===' -ForegroundColor Cyan
$lista = dotnet user-secrets list --project $projeto
foreach ($linha in $lista) { Write-Host "  $linha" -ForegroundColor DarkGray }

Write-Host ''
Write-Host 'Pronto. Guarde estas credenciais - sao o seu login de admin:' -ForegroundColor Yellow
Write-Host "  email:  $OwnerEmail" -ForegroundColor Yellow
Write-Host "  senha:  $OwnerPassword" -ForegroundColor Yellow
Write-Host ''
Write-Host 'Proximo passo:' -ForegroundColor Cyan
Write-Host '  $env:ConnectionStrings__Default = "<string da branch dev>"' -ForegroundColor DarkGray
Write-Host '  .\dev-local.ps1' -ForegroundColor DarkGray
Write-Host ''
Write-Host 'Obs: Lease:PrivateKey nao foi gerada. So e necessaria para os endpoints' -ForegroundColor DarkGray
Write-Host 'de licenca (/v1/license/*). Login e admin funcionam sem ela.' -ForegroundColor DarkGray
Write-Host ''

# dev-local.ps1 - sobe o backend local apontando para a branch "dev" do Neon.
#
# Uso:
#   $env:ConnectionStrings__Default = "<string .NET da branch dev, copiada do Neon>"
#   .\dev-local.ps1
#
# O script carrega Jwt:Key, Lease:PrivateKey e Seed:Owner:* do user-secrets e os
# expoe como variaveis de ambiente. A connection string NAO vem do user-secrets:
# a de la aponta para producao, entao ela tem que vir de fora, por variavel.

$ErrorActionPreference = 'Stop'

# Endpoint de producao. Se a connection string bater com isso, o script aborta.
$HostProducao = 'ep-falling-cloud-acvon70x'

Write-Host ''
Write-Host '=== 1. Carregando segredos do user-secrets ===' -ForegroundColor Cyan

# Descobre o UserSecretsId pelo proprio csproj - sem GUID digitado a mao.
$csproj = Join-Path $PSScriptRoot 'src\Plataforma.Api\Plataforma.Api.csproj'
if (-not (Test-Path $csproj)) {
    Write-Host "  X csproj nao encontrado em: $csproj" -ForegroundColor Red
    Write-Host '    Rode o script de dentro da pasta backend.' -ForegroundColor Yellow
    exit 1
}

$id = ([xml](Get-Content $csproj)).Project.PropertyGroup.UserSecretsId |
      Where-Object { $_ } | Select-Object -First 1

$arquivo = Join-Path $env:APPDATA "Microsoft\UserSecrets\$id\secrets.json"

if (-not (Test-Path $arquivo)) {
    # Plano B: procura qualquer secrets.json na pasta do usuario.
    $achado = Get-ChildItem (Join-Path $env:APPDATA 'Microsoft\UserSecrets') -Recurse -Filter 'secrets.json' -ErrorAction SilentlyContinue |
              Select-Object -First 1
    if (-not $achado) {
        Write-Host '  X secrets.json nao encontrado.' -ForegroundColor Red
        Write-Host "    Procurei em: $arquivo" -ForegroundColor Yellow
        exit 1
    }
    $arquivo = $achado.FullName
}

Write-Host "  arquivo: $arquivo" -ForegroundColor DarkGray

$segredos = Get-Content $arquivo -Raw | ConvertFrom-Json

foreach ($prop in $segredos.PSObject.Properties) {
    # Pula a connection string: a do user-secrets e a de PRODUCAO.
    if ($prop.Name -eq 'ConnectionStrings:Default') {
        Write-Host "  -- $($prop.Name) (ignorado - e producao)" -ForegroundColor DarkYellow
        continue
    }
    $nome = $prop.Name -replace ':', '__'
    Set-Item -Path "env:$nome" -Value $prop.Value
    Write-Host "  ok $($prop.Name)" -ForegroundColor Green
}

Write-Host ''
Write-Host '=== 2. Conferindo para qual banco vamos apontar ===' -ForegroundColor Cyan

$conn = $env:ConnectionStrings__Default

if (-not $conn) {
    Write-Host '  X ConnectionStrings__Default nao esta definida nesta janela.' -ForegroundColor Red
    Write-Host ''
    Write-Host '    Rode antes (colando a string copiada do Neon):' -ForegroundColor Yellow
    Write-Host '    $env:ConnectionStrings__Default = "Host=ep-proud-shadow-...;Database=neondb;..."' -ForegroundColor Yellow
    exit 1
}

if ($conn -match $HostProducao) {
    Write-Host '  X PARADO. Essa connection string aponta para PRODUCAO.' -ForegroundColor Red
    Write-Host '    Copie a string da branch dev no console do Neon.' -ForegroundColor Yellow
    exit 1
}

Write-Host ("  " + ($conn -replace 'Password=[^;]*', 'Password=***')) -ForegroundColor DarkGray
Write-Host '  ok nao e producao' -ForegroundColor Green

Write-Host ''
Write-Host '=== 3. Subindo o backend ===' -ForegroundColor Cyan
Write-Host '  Swagger: http://localhost:5077/swagger' -ForegroundColor Yellow
Write-Host '  Para parar: Ctrl+C' -ForegroundColor DarkGray
Write-Host ''

# Development e o que habilita o Swagger no Program.cs.
$env:ASPNETCORE_ENVIRONMENT = 'Development'

dotnet run --project src/Plataforma.Api --urls http://localhost:5077

# ============================================================
#  criar-usuarios.ps1 - cria varias contas de uma vez no backend
#  Filippon (POST /v1/admin/users). REQUER papel Owner.
#
#  VOCE roda este script; a senha do Owner e' digitada no terminal
#  e NUNCA fica salva. Os e-mails vem de 'usuarios.txt' (1 por linha).
#
#  Uso:
#    .\criar-usuarios.ps1 -Senha "SenhaCompartilhada123"
#    .\criar-usuarios.ps1 -Senha "..." -Papel Customer -Lista .\usuarios.txt
# ============================================================
param(
  [Parameter(Mandatory=$true)] [string] $Senha,   # senha IGUAL p/ todos (min 8)
  [string] $Papel  = "Customer",                  # Customer | Support | Owner
  [string] $Lista  = (Join-Path $PSScriptRoot "usuarios.txt"),
  [string] $BaseUrl = "https://plataforma-backend-production-d2a4.up.railway.app"
)
$ErrorActionPreference = "Stop"

if ($Senha.Length -lt 8) { Write-Host "A senha precisa ter ao menos 8 caracteres." -ForegroundColor Red; exit 1 }
if (-not (Test-Path $Lista)) { Write-Host "Lista nao encontrada: $Lista" -ForegroundColor Red; exit 1 }

# O -BaseUrl padrao e PRODUCAO. Rodar um teste sem lembrar disso cria contas no
# banco dos clientes - e conta criada nao se desfaz sozinha. Confirmacao
# explicita para qualquer alvo que nao seja a propria maquina.
$ehLocal = $false
try { $ehLocal = ([Uri]$BaseUrl).Host -in @('localhost','127.0.0.1','::1') } catch { }
if (-not $ehLocal) {
  Write-Host ""
  Write-Host "  ATENCAO: alvo de PRODUCAO" -ForegroundColor Yellow
  Write-Host "  $BaseUrl" -ForegroundColor Yellow
  Write-Host "  Lista: $Lista" -ForegroundColor Yellow
  Write-Host "  Papel: $Papel" -ForegroundColor Yellow
  Write-Host ""
  $ok = Read-Host "  Criar estas contas em PRODUCAO? (digite SIM)"
  if ($ok -ne 'SIM') { Write-Host "  Cancelado." -ForegroundColor Cyan; exit 0 }
  Write-Host ""
}

# 1) Login do Owner — a senha e' digitada aqui, nao fica no script nem no historico.
$ownerEmail = Read-Host "E-mail do Owner"
$ownerSec   = Read-Host "Senha do Owner" -AsSecureString
$ownerPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                [Runtime.InteropServices.Marshal]::SecureStringToBSTR($ownerSec))
try {
  $login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/auth/login" `
    -ContentType "application/json" `
    -Body (@{ email = $ownerEmail; password = $ownerPlain } | ConvertTo-Json)
} catch {
  Write-Host "Falha no login do Owner. Confira e-mail/senha." -ForegroundColor Red; exit 1
}
$token = $login.access_token
if (-not $token) { Write-Host "Login sem token (a conta e' Owner?)." -ForegroundColor Red; exit 1 }
$headers = @{ Authorization = "Bearer $token" }

# 2) Cria cada e-mail da lista (ignora linhas vazias e comentarios com #).
$emails = Get-Content $Lista | ForEach-Object { $_.Trim() } |
          Where-Object { $_ -and -not $_.StartsWith("#") }
$ok = 0; $existe = 0; $falha = 0
foreach ($email in $emails) {
  $body = @{ email = $email; password = $Senha; role = $Papel } | ConvertTo-Json
  try {
    Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/admin/users" -Headers $headers `
      -ContentType "application/json" -Body $body | Out-Null
    Write-Host ("OK        " + $email) -ForegroundColor Green; $ok++
  } catch {
    $resp = $_.Exception.Response
    $code = if ($resp) { [int]$resp.StatusCode } else { 0 }
    if ($code -eq 409) { Write-Host ("JA EXISTE " + $email) -ForegroundColor Yellow; $existe++ }
    else {
      $msg = ""
      try { $sr = New-Object IO.StreamReader($resp.GetResponseStream()); $msg = $sr.ReadToEnd() } catch {}
      Write-Host ("FALHA     " + $email + "  [$code] " + $msg) -ForegroundColor Red; $falha++
    }
  }
}
Write-Host ""
Write-Host ("Criados: $ok  |  Ja existiam: $existe  |  Falhas: $falha") -ForegroundColor Cyan
Write-Host ("Papel: $Papel  |  Senha (igual p/ todos): $Senha") -ForegroundColor DarkGray

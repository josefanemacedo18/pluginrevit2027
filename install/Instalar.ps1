<#
  Instala o DetalhaBIM para o usuário atual — uso opcional, para quem compila a partir do
  código-fonte. Usuários finais: basta copiar os arquivos (veja COMO-INSTALAR.txt).
#>
param([string]$RevitVersion = "2027")
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$pacote = @(
    (Join-Path $here "COPIAR PARA Addins 2027"),
    (Join-Path $here "..\src\DetalhaBIM\bin\Release\net10.0-windows\Pacote"),
    (Join-Path $here "..\src\DetalhaBIM\bin\Debug\net10.0-windows\Pacote")
) | Where-Object { Test-Path (Join-Path $_ "DetalhaBIM.addin") } | Select-Object -First 1

if (-not $pacote) {
    Write-Host "Pacote não encontrado. Compile antes com: dotnet build -c Release" -ForegroundColor Red
    exit 1
}
if (Get-Process -Name "Revit" -ErrorAction SilentlyContinue) {
    Write-Host "Feche o Revit antes de instalar/atualizar o DetalhaBIM." -ForegroundColor Yellow
    exit 1
}

$dest = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $pacote "*") $dest -Recurse -Force
Get-ChildItem (Join-Path $dest "DetalhaBIM") -Recurse -File | Unblock-File
Unblock-File (Join-Path $dest "DetalhaBIM.addin")

# Remove o pacote .bundle das versões 1.1.0/1.1.1, para não carregar duas vezes.
Remove-Item (Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\DetalhaBIM.bundle") -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "DetalhaBIM instalado em: $dest" -ForegroundColor Green
Write-Host "Abra o Revit $RevitVersion e escolha 'Sempre carregar' na mensagem de segurança."

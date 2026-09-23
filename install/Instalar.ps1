<#
  Instala o DetalhaBIM (pacote .bundle) para o usuário atual — uso opcional, para quem compila
  a partir do código-fonte. Usuários finais: basta copiar a pasta DetalhaBIM.bundle
  (veja COMO-INSTALAR.txt); nenhum script é necessário.
#>
param([string]$RevitVersion = "2027")
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$bundle = @(
    (Join-Path $here "DetalhaBIM.bundle"),
    (Join-Path $here "..\src\DetalhaBIM\bin\Release\net10.0-windows\DetalhaBIM.bundle"),
    (Join-Path $here "..\src\DetalhaBIM\bin\Debug\net10.0-windows\DetalhaBIM.bundle")
) | Where-Object { Test-Path (Join-Path $_ "PackageContents.xml") } | Select-Object -First 1

if (-not $bundle) {
    Write-Host "Pasta DetalhaBIM.bundle não encontrada. Compile antes com: dotnet build -c Release" -ForegroundColor Red
    exit 1
}
if (Get-Process -Name "Revit" -ErrorAction SilentlyContinue) {
    Write-Host "Feche o Revit antes de instalar/atualizar o DetalhaBIM." -ForegroundColor Yellow
    exit 1
}

$plugins = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
$dest = Join-Path $plugins "DetalhaBIM.bundle"
New-Item -ItemType Directory -Force -Path $plugins | Out-Null
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Copy-Item $bundle $dest -Recurse -Force
Get-ChildItem $dest -Recurse -File | Unblock-File

# Remove a instalação antiga (versão 1.0, em Addins\2027) para não carregar o plugin duas vezes.
$old = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
Remove-Item (Join-Path $old "DetalhaBIM.addin") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $old "DetalhaBIM") -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "DetalhaBIM instalado em: $dest" -ForegroundColor Green
Write-Host "Abra o Revit $RevitVersion e escolha 'Sempre carregar' na mensagem de segurança."

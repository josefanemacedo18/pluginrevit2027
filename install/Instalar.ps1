<#
  Instala o DetalhaBIM no Revit 2027 (somente para o usuário atual, sem precisar de administrador).
  Uso:  clique com o botão direito > "Executar com o PowerShell"  (ou use Instalar.bat)
#>
param([string]$RevitVersion = "2027")
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-First([string[]]$paths) {
    foreach ($p in $paths) { if ($p -and (Test-Path $p)) { return (Resolve-Path $p).Path } }
    return $null
}

# Funciona tanto a partir do pacote baixado quanto a partir do código-fonte compilado.
$dll = Find-First @(
    (Join-Path $here "DetalhaBIM\DetalhaBIM.dll"),
    (Join-Path $here "..\src\DetalhaBIM\bin\Release\net10.0-windows\DetalhaBIM.dll"),
    (Join-Path $here "..\src\DetalhaBIM\bin\Debug\net10.0-windows\DetalhaBIM.dll")
)
$addin = Find-First @(
    (Join-Path $here "DetalhaBIM.addin"),
    (Join-Path $here "..\src\DetalhaBIM\DetalhaBIM.addin")
)
if (-not $dll -or -not $addin) {
    Write-Host "DetalhaBIM.dll não encontrado. Compile antes com: dotnet build -c Release" -ForegroundColor Red
    exit 1
}

if (Get-Process -Name "Revit" -ErrorAction SilentlyContinue) {
    Write-Host "Feche o Revit antes de instalar/atualizar o DetalhaBIM." -ForegroundColor Yellow
    exit 1
}

$dest = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$pluginDir = Join-Path $dest "DetalhaBIM"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null

Copy-Item $addin $dest -Force
Copy-Item $dll $pluginDir -Force
$pdb = [IO.Path]::ChangeExtension($dll, ".pdb")
if (Test-Path $pdb) { Copy-Item $pdb $pluginDir -Force }

# Arquivos baixados da internet são bloqueados pelo Windows; o Revit não carrega DLL bloqueada.
Get-ChildItem $pluginDir -Recurse | Unblock-File
Unblock-File (Join-Path $dest "DetalhaBIM.addin")

Write-Host ""
Write-Host "DetalhaBIM instalado com sucesso em:" -ForegroundColor Green
Write-Host "  $dest"
Write-Host ""
Write-Host "Abra o Revit $RevitVersion e escolha 'Sempre carregar' na mensagem de segurança."
Write-Host "A aba 'DetalhaBIM' aparecerá na faixa de opções."

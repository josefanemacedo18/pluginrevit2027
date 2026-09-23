<# Remove o DetalhaBIM. As configurações em %AppData%\DetalhaBIM são mantidas. #>
param([string]$RevitVersion = "2027")
if (Get-Process -Name "Revit" -ErrorAction SilentlyContinue) {
    Write-Host "Feche o Revit antes de desinstalar." -ForegroundColor Yellow
    exit 1
}
Remove-Item (Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\DetalhaBIM.bundle") -Recurse -Force -ErrorAction SilentlyContinue
$old = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
Remove-Item (Join-Path $old "DetalhaBIM.addin") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $old "DetalhaBIM") -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "DetalhaBIM removido. (Configurações preservadas em $env:APPDATA\DetalhaBIM)" -ForegroundColor Green

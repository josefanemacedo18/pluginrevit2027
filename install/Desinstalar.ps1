<# Remove o DetalhaBIM do Revit 2027. As configurações em %AppData%\DetalhaBIM são mantidas. #>
param([string]$RevitVersion = "2027")
$dest = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
if (Get-Process -Name "Revit" -ErrorAction SilentlyContinue) {
    Write-Host "Feche o Revit antes de desinstalar." -ForegroundColor Yellow
    exit 1
}
Remove-Item (Join-Path $dest "DetalhaBIM.addin") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dest "DetalhaBIM") -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "DetalhaBIM removido. (Configurações preservadas em $env:APPDATA\DetalhaBIM)" -ForegroundColor Green

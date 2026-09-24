<# Remove o DetalhaBIM. As configurações em %AppData%\DetalhaBIM são mantidas. #>
param([string]$RevitVersion = "2027")
if (Get-Process -Name "Revit" -ErrorAction SilentlyContinue) {
    Write-Host "Feche o Revit antes de desinstalar." -ForegroundColor Yellow
    exit 1
}
$addins = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
Remove-Item (Join-Path $addins "DetalhaBIM.addin") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $addins "DetalhaBIM.dll") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $addins "DetalhaBIM") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\DetalhaBIM.bundle") -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "DetalhaBIM removido. (Configurações preservadas em $env:APPDATA\DetalhaBIM)" -ForegroundColor Green

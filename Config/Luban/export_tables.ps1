$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")

Write-Host "Exporting Luban tables..."
Write-Host "Project root: $root"

& (Join-Path $root "Tools\gen_luban.ps1") @args
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "Export complete."
    Write-Host "Code: $(Join-Path $root 'Assets\Scripts\Generated\Luban')"
    Write-Host "Data: $(Join-Path $root 'Assets\Resources\Config\Luban\Bin')"
} else {
    Write-Host ""
    Write-Host "Export failed. Exit code: $exitCode"
}

exit $exitCode

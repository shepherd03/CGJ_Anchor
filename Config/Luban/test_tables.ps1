$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$project = Join-Path $PSScriptRoot "TestValidator\TableConfigValidator.csproj"

Write-Host "Validating generated Luban table data..."
Write-Host "Project root: $root"

& dotnet run --project $project -c Release
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "Validation complete."
} else {
    Write-Host ""
    Write-Host "Validation failed. Exit code: $exitCode"
}

exit $exitCode

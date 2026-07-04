$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    & "$root\Tools\luban.ps1" `
        --conf "$root\Config\Luban\luban.conf" `
        -t client `
        -c cs-bin `
        -d bin `
        -x "outputCodeDir=Assets/Scripts/Generated/Luban" `
        -x "outputDataDir=Assets/Resources/Config/Luban/Bin" `
        @args

    exit $LASTEXITCODE
}
finally {
    Pop-Location
}

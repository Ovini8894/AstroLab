param(
    [string]$ProjectId = '833d97f9-724e-4dc5-beda-3edd0c5b2007',
    [string]$EnvironmentName = 'production'
)
$ErrorActionPreference = 'Stop'
$catalogPath = Join-Path $PSScriptRoot '../Resources/base_knowledge.json'
$catalogText = Get-Content -Raw -LiteralPath $catalogPath
$catalog = $catalogText | ConvertFrom-Json
if ($catalog.schemaVersion -ne 1 -or @($catalog.equipment).Count -ne 7) { throw 'Catálogo incompleto.' }
# Authenticate through the supported UGS login flow first. This script never reads Editor tokens.
& unity ugs cloud-save data custom set --project-id $ProjectId --environment-name $EnvironmentName --custom-id astrolab_catalog_v1 --key catalog --value $catalogText --json
if ($LASTEXITCODE -ne 0) { throw 'A publicação falhou. Confira a autenticação oficial e as permissões do projeto.' }
$readBack = & unity ugs cloud-save data custom get --project-id $ProjectId --environment-name $EnvironmentName --custom-id astrolab_catalog_v1 --keys catalog --json
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível confirmar a leitura após publicar.' }
$readBack

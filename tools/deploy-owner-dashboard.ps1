$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectRef = 'aafdjzdirqpanuueveng'
$migration = Join-Path $repoRoot 'supabase/migrations/202610180001_app_maintenance.sql'
$function = Join-Path $repoRoot 'supabase/functions/aetherion-license-admin/index.ts'

if (-not (Test-Path -LiteralPath $migration)) {
    throw "Migration fehlt: $migration"
}
if (-not (Test-Path -LiteralPath $function)) {
    throw "Edge Function fehlt: $function"
}

Set-Location $repoRoot

Write-Host 'Spiele die Wartungs-Migration auf dem verknüpften Supabase-Projekt ein ...'
& npx.cmd --yes supabase db push --linked --yes
if ($LASTEXITCODE -ne 0) {
    throw "Supabase-Migration fehlgeschlagen (Exitcode $LASTEXITCODE). Prüfe Login und Migrationstatus."
}

Write-Host 'Stelle die Owner-Dashboard-Edge-Function bereit ...'
& npx.cmd --yes supabase functions deploy aetherion-license-admin --project-ref $projectRef
if ($LASTEXITCODE -ne 0) {
    throw "Edge-Function-Deployment fehlgeschlagen (Exitcode $LASTEXITCODE)."
}

foreach ($functionName in @('aetherion-license-check', 'aetherion-app-status')) {
    & npx.cmd --yes supabase functions deploy $functionName --project-ref $projectRef
    if ($LASTEXITCODE -ne 0) { throw "Deployment fehlgeschlagen: $functionName" }
}
Write-Host 'Owner-Dashboard und Wartungs-Backend sind aktualisiert. Der Wartungsstatus wird nicht geändert.' -ForegroundColor Green

Write-Host "Merhaba! Bu bir PowerShell scriptidir."
Write-Host "Aldığın parametreler:"
foreach ($arg in $args) {
    Write-Host "  - $arg"
}
if ($args.Count -eq 0) {
    Write-Host "  (hiç parametre yok)"
}
exit 0

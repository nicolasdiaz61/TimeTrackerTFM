<#
.SYNOPSIS
    Comprueba que un paquete alcanza el porcentaje mínimo de cobertura de líneas y de ramas.
.DESCRIPTION
    Lee todos los coverage.cobertura.xml bajo -ResultsDirectory y toma, para el paquete, el MEJOR
    line-rate y branch-rate entre informes (el paquete también aparece, con poca cobertura, en los
    informes de otros proyectos de pruebas). Un paquete sin ramas cuenta como 100 % de ramas.
    Termina con código distinto de 0 y un mensaje claro si no llega al mínimo o no hay datos.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ResultsDirectory,
    [Parameter(Mandatory)] [string] $Package,
    [Parameter(Mandatory)] [double] $MinimumPercent
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ResultsDirectory)) {
    Write-Error "No existe el directorio de resultados '$ResultsDirectory'. Ejecute antes dotnet test con --collect:'XPlat Code Coverage'."
    exit 1
}

$reports = Get-ChildItem -Path $ResultsDirectory -Recurse -Filter coverage.cobertura.xml
$bestLine = -1.0
$bestBranch = -1.0

foreach ($report in $reports) {
    $node = ([xml](Get-Content -Raw $report.FullName)).SelectSingleNode("//package[@name='$Package']")
    if ($null -eq $node) { continue }

    $line = [double]::Parse($node.GetAttribute('line-rate'), [cultureinfo]::InvariantCulture) * 100
    $hasBranches = $null -ne $node.SelectSingleNode(".//line[@branch='True']")
    $branch = if ($hasBranches) { [double]::Parse($node.GetAttribute('branch-rate'), [cultureinfo]::InvariantCulture) * 100 } else { 100.0 }

    $bestLine = [Math]::Max($bestLine, $line)
    $bestBranch = [Math]::Max($bestBranch, $branch)
}

if ($bestLine -lt 0) {
    Write-Error "No se encontró el paquete '$Package' en ningún informe de '$ResultsDirectory' ($($reports.Count) informes)."
    exit 1
}

Write-Host ("Paquete {0}: líneas {1:N1} %, ramas {2:N1} % (mínimo {3} %)" -f $Package, $bestLine, $bestBranch, $MinimumPercent)

if ($bestLine -lt $MinimumPercent -or $bestBranch -lt $MinimumPercent) {
    Write-Error "Cobertura insuficiente en '$Package': se exige al menos $MinimumPercent %."
    exit 1
}

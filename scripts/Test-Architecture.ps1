param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$violations = [System.Collections.Generic.List[string]]::new()

foreach ($project in @('VantaiViet.CoreApi', 'VantaiViet.MatchingApi')) {
    $projectRoot = Join-Path $root "src/$project"
    foreach ($file in Get-ChildItem (Join-Path $projectRoot 'Controllers') -Filter *.cs -Recurse) {
        $source = Get-Content $file.FullName -Raw
        if ($source -match 'VantaiViet\.[\w]+\.Data\b|\.Entities\b|DbContext|Repository|QueryService|UnitOfWork|HttpClient|SaveChanges|\.Where\(|\.Select\(') {
            $violations.Add("Controller has a forbidden dependency or operation: $($file.FullName)")
        }
    }
    foreach ($file in Get-ChildItem (Join-Path $projectRoot 'Entities') -Filter *.cs -Recurse) {
        $source = Get-Content $file.FullName -Raw
        # IdentityUser is a persistence model, not a controller/service dependency.
        if ($source -match 'Microsoft\.AspNetCore\.(?!Identity\b)|VantaiViet\.[\w]+\.(Services|Hosting|Controllers)') {
            $violations.Add("Entity depends on an application component: $($file.FullName)")
        }
    }
    $projectFile = Get-Content (Join-Path $projectRoot "$project.csproj") -Raw
    if ($projectFile -match '<ProjectReference[^>]+VantaiViet\.(CoreApi|MatchingApi)') {
        $violations.Add("Executable project reference is forbidden: $project")
    }
}

if ($violations.Count -gt 0) { throw ($violations -join [Environment]::NewLine) }
Write-Host 'Architecture source guard passed. This is a lightweight guard, not full dependency analysis.'

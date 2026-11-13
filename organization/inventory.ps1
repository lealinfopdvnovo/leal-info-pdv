$ErrorActionPreference='Stop'
$results=@()
foreach($id in @('GLOBAL','001','002','003','004')){
  # Separate PowerShell process avoids assembly identity reuse across distributions.
  $env:INVENTORY_ID=$id
  $json=& pwsh -NoProfile -File organization/inventory_resources.ps1
  if($LASTEXITCODE -ne 0){throw "Resource inventory failed $id"}
  $results+=($json|ConvertFrom-Json)
}
$results|ConvertTo-Json -Depth 20|Set-Content organization-evidence/embedded-components.json

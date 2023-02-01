$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet)) {
  Write-Error "dotnet command not found."
  exit 1
}

$workDir = Split-Path $PSScriptRoot -Parent
$slnFiles = Get-ChildItem -Filter "*.sln" $workDir
if ($slnFiles.Count -ne 1) {
  Write-Error "There should be only one solution file in the project directory."
  exit 1
}
$sln = $slnFiles[0]

# FIXME: Projects can be more than one
$proj = (
  dotnet sln $sln list `
  | Select-String -NotMatch -Pattern Libplanet\.Headless.csproj`$ `
  | Select-String -Pattern \.csproj`$
).Line
$projectName = Split-Path -LeafBase $proj

if ((dotnet tool list --local `
     | Select-String -Pattern libplanet.tools).Count -lt 1) {
  Write-Error "Libplanet.Tools is not installed."
  exit 1
}

$configuration = "Debug"
$targetFramework = (Select-Xml `
  -XPath "/Project/PropertyGroup/TargetFramework/text()" `
  (Join-Path $workDir $proj)).Node.InnerText
dotnet build --configuration $configuration $sln
$dataDir = Join-Path $workDir "data"
New-Item -ItemType Directory -Force $dataDir

$keyId = ((dotnet planet key create --passphrase=) -split '\s+')[0]
try {
  $genesisPath = Join-Path $dataDir "genesis.bin"
  $binPath = Join-Path `
    $workDir `
    $projectName `
    "bin" `
    $configuration `
    $targetFramework
  $dllPath = Join-Path $binPath "$projectName.dll"
  dotnet planet block generate-genesis `
    --passphrase="" `
    --load-assembly=$dllPath `
    --policy-factory="$projectName.BlockPolicySource.GetPolicy" `
  $keyId `
  $genesisPath

  Write-Information "Genesis block is generated at $genesisPath."

  $storeDir = Join-Path $dataDir "store"
New-Item -ItemType Directory -Force $storeDir

  $genesisUri = "file://$(Resolve-Path $genesisPath)"
  $storeUri = "rocksdb+file://$(Resolve-Path $storeDir)?secure=false"
  $appSettings = Get-ChildItem `
  -Filter "appsettings*.json" `
  (Join-Path $workDir $projectName)
  foreach ($file in $appSettings) {
      Get-Content $file `
      | ConvertFrom-Json `
      | Add-Member `
          -Name "GenesisBlockPath" `
          -Value $genesisUri `
          -MemberType NoteProperty `
          -Force `
          -PassThru `
      | Add-Member `
          -Name "StoreUri" `
          -Value $storeUri `
          -MemberType NoteProperty `
          -Force `
          -PassThru `
      | ConvertTo-Json -Depth 100 `
      | Set-Content -Encoding utf8 $file
  }

  Remove-Item -Recurse $PSScriptRoot -ErrorAction SilentlyContinue
} finally {
  dotnet planet key remove --passphrase="" $keyId
}

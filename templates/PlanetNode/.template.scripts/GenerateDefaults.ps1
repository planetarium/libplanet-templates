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

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
  Write-Error "Failed to restore tools."
  exit $LASTEXITCODE
}

# FIXME: Projects can be more than one
$proj = (
  dotnet sln $sln list `
  | Select-String -NotMatch -Pattern Libplanet\.Headless.csproj`$ `
  | Select-String -Pattern \.csproj`$
).Line
if ($LASTEXITCODE -ne 0) {
  Write-Error "Failed to list projects."
  exit $LASTEXITCODE
}
$projectName = Split-Path -LeafBase $proj

if ((dotnet tool list --local `
     | Select-String -Pattern libplanet.tools).Count -lt 1) {
  Write-Error "Libplanet.Tools is not installed."
  exit 1
}

if ($LASTEXITCODE -ne 0) {
  Write-Error "Failed to list tools."
  exit $LASTEXITCODE
}

$configuration = "Debug"
$targetFramework = (Select-Xml `
  -XPath "/Project/PropertyGroup/TargetFramework/text()" `
  (Join-Path $workDir $proj)).Node.InnerText
dotnet build --configuration $configuration $sln
if ($LASTEXITCODE -ne 0) {
  Write-Error "Failed to build the project."
  exit $LASTEXITCODE
}

$dataDir = Join-Path $workDir "data"
New-Item -ItemType Directory -Force $dataDir

$randomBytes = New-Object byte[](32)
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($randomBytes)
$passphrase = ($randomBytes | ForEach-Object ToString X2) -join ''

$keyId = ((dotnet planet key create --passphrase="$passphrase") -split '\s+')[0]
if ($LASTEXITCODE -ne 0) {
  Write-Error "Failed to create a temporary private key."
  exit $LASTEXITCODE
}

$publicKey = dotnet planet key export `
  --public-key `
  --passphrase="$passphrase" `
  "$keyId"
if ($LASTEXITCODE -ne 0) {
  Write-Error "Failed to derive the public key from the private key."
  exit $LASTEXITCODE
}

$address = ((dotnet planet key list 2>$null `
  | Select-String $keyId) -split " ")[1]

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
    --passphrase="$passphrase" `
    --load-assembly=$dllPath `
    --policy-factory="$projectName.BlockPolicySource.GetPolicy" `
    -v "$publicKey" `
    "$keyId" `
    $genesisPath
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to generate the genesis block."
    exit $LASTEXITCODE
  }

  Write-Information "Genesis block is generated at $genesisPath."

  $apv = dotnet planet apv sign --passphrase="$passphrase" "$keyId" 0
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to sign an APV."
    exit $LASTEXITCODE
  }

  $storeDirPrefix = Join-Path $dataDir "store"
  $genesisUri = "file://$(Resolve-Path $genesisPath)"
  $appSettings = Get-ChildItem `
    -Filter "appsettings*.json" `
    (Join-Path $workDir $projectName)
  foreach ($file in $appSettings) {
    $configType = if ($file.Name -match '^appsettings\.(\w+)\.json$') {
      $Matches.1
    } else {
      $null
    }
    $storeDir = if ($null -eq $configType) {
      $storeDirPrefix
    } else {
      "${storeDirPrefix}-$configType"
    }
    New-Item -ItemType Directory -Force $storeDir
    $storeUri = "rocksdb+file://$(Resolve-Path $storeDir)?secure=false"
    $configJson = Get-Content $file | ConvertFrom-Json
    $configJson = $configJson `
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
          -PassThru
    $configJson.Network = $configJson.Network `
      | Add-Member `
          -Name "AppProtocolVersion" `
          -Value $apv `
          -MemberType NoteProperty `
          -Force `
          -PassThru `
      | Add-Member `
          -Name "TrustedAppProtocolVersionSigners" `
          -Value @($publicKey) `
          -MemberType NoteProperty `
          -Force `
          -PassThru
    if ($null -ne $configType) {
      $configJson.Network = $configJson.Network `
        | Add-Member `
            -Name "PeerStrings" `
            -Value @("$publicKey,localhost,31234") `
            -MemberType NoteProperty `
            -Force `
            -PassThru
    }
    $configJson `
      | ConvertTo-Json -Depth 100 `
      | Set-Content -Encoding utf8 $file
  }

  $keyInfoText = "A private key that can be used for validation is generated.`n`n" + `
    "- Key ID: ``$keyId```n" + `
    "- Address: ``$address```n" + `
    "- Passphrase: ``$passphrase```n`n" + `
    "----`n`n"
  $readmePath = Join-Path $workDir "README.md"
  $readmeText = $keyInfoText + (Get-Content -Encoding utf8 $readmePath -Raw)
  $readmeText | Set-Content -Encoding utf8 $readmePath

  Remove-Item -Recurse $PSScriptRoot -ErrorAction SilentlyContinue
} catch {
  dotnet planet key remove --passphrase="$passphrase" "$keyId"
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to remove the temporary private key: $keyId"
    exit $LASTEXITCODE
  }
}

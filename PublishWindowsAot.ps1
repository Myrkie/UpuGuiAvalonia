$DotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue

if (-not $DotnetCommand) {
    throw ".NET SDK is not installed or could not be found in PATH. Please install the .NET SDK."
}

Write-Host "Checking .NET SDK availability..."

$DotnetOutput = & dotnet --info 2>&1
$DotnetExitCode = $LASTEXITCODE

if ($DotnetExitCode -ne 0) {
    Write-Host ""
    Write-Host ".NET SDK is installed, but dotnet is unavailable." -ForegroundColor Red
    Write-Host "Make sure the .NET SDK is installed correctly."
    Write-Host ""

    if ($DotnetOutput) {
        Write-Host ($DotnetOutput | Out-String)
    }

    throw ".NET SDK is unavailable. Check your .NET installation and try again."
}

$Config = "Release"

$ProjectDir = Join-Path $PSScriptRoot "UpuGuiAvalonia"
$PublishDir = Join-Path $PSScriptRoot "Builds\Windows"

Write-Host "Publishing Windows x64 build..."

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

$PublishDir = (Resolve-Path $PublishDir).Path

dotnet publish (Join-Path $ProjectDir "UpuGuiAvalonia.csproj") `
    -c $Config `
    -r win-x64 `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    throw "Windows publish failed with exit code $LASTEXITCODE. Check the build output above."
}

Write-Host ""
Write-Host "Windows publish completed: $PublishDir" -ForegroundColor Green
# Release automation: bump version -> build self-contained exe + installer -> commit/push -> GitHub release with assets.
# Usage:  ./release.ps1 2.0.1  [-NoGit]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$NoGit
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition

Write-Host "== Bumping version to $Version ==" -ForegroundColor Cyan
$csproj = "$root\app\Parrot.csproj"
(Get-Content $csproj -Raw) -replace '<Version>.*?</Version>', "<Version>$Version</Version>" | Set-Content $csproj -Encoding UTF8
$inst = "$root\installer\Installer.cs"
if (Test-Path $inst) {
    (Get-Content $inst -Raw) -replace 'public const string Version = "[^"]*";', "public const string Version = `"$Version`";" | Set-Content $inst -Encoding UTF8
}

Write-Host "== Building ==" -ForegroundColor Cyan
& "$root\build.ps1"
if ($LASTEXITCODE -ne 0) { throw "build failed" }
$setup = "$root\dist\Parrot-Setup.exe"
$app = "$root\dist\Parrot.exe"
if (-not (Test-Path $setup)) { throw "setup exe missing" }

# git/gh write progress to stderr; don't let that abort the script.
$ErrorActionPreference = 'Continue'

if (-not $NoGit) {
    Write-Host "== Commit + push ==" -ForegroundColor Cyan
    git -C $root add -A
    git -C $root commit -m "release: v$Version" 2>&1 | Out-Null
    git -C $root push 2>&1 | Out-Null
}

Write-Host "== GitHub release v$Version ==" -ForegroundColor Cyan
$notes = "Parrot v$Version - 자동 업데이트 대상 릴리스. Parrot-Setup.exe 실행으로 설치/업데이트."
$exists = (gh release view "v$Version" 2>$null)
if ($exists) {
    gh release upload "v$Version" $setup $app --clobber
} else {
    gh release create "v$Version" $setup $app --title "Parrot v$Version" --notes $notes
}
Write-Host "DONE: https://github.com/devRavit/Parrot/releases/tag/v$Version" -ForegroundColor Green

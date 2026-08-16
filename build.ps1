# Build Parrot: self-contained app exe + self-contained installer (Setup.exe)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet).Source }

Write-Host "== [0/4] Compiling native hook DLL (ParrotHook64.dll) ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Force "$root\dist" | Out-Null
$gcc = (Get-Command gcc -ErrorAction SilentlyContinue).Source
if (-not $gcc) {
    $found = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter gcc.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $gcc = $found.FullName }
}
if ($gcc) {
    $mh = "$root\native\minhook-master"
    & $gcc -O2 -shared -static -static-libgcc -o "$root\dist\ParrotHook64.dll" `
        "$root\native\hookdll.c" "$mh\src\buffer.c" "$mh\src\hook.c" "$mh\src\trampoline.c" "$mh\src\hde\hde64.c" `
        -I "$mh\include" -luser32 -lgdi32
    if ($LASTEXITCODE -ne 0) { throw "native DLL compile failed" }
    Write-Host "   dist\ParrotHook64.dll compiled"
} elseif (Test-Path "$root\dist\ParrotHook64.dll") {
    Write-Host "   gcc not found; using existing dist\ParrotHook64.dll" -ForegroundColor Yellow
} else {
    throw "gcc (mingw-w64) not found and no prebuilt dist\ParrotHook64.dll. Install: winget install BrechtSanders.WinLibs.POSIX.UCRT"
}

Write-Host "== [1/4] Publishing app (self-contained single-file) ==" -ForegroundColor Cyan
& $dotnet publish "$root\app\Parrot.csproj" -c Release -r win-x64 --nologo
if ($LASTEXITCODE -ne 0) { throw "app publish failed" }

$appExe = "$root\app\bin\Release\net10.0-windows\win-x64\publish\Parrot.exe"
if (-not (Test-Path $appExe)) { throw "app exe not found: $appExe" }
Write-Host "   app exe: $appExe ($([math]::Round((Get-Item $appExe).Length/1MB,1)) MB)"

Write-Host "== [2/4] Staging payload for installer ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Force "$root\installer\payload" | Out-Null
Copy-Item $appExe "$root\installer\payload\Parrot.exe" -Force

Write-Host "== [3/4] Publishing installer (self-contained single-file) ==" -ForegroundColor Cyan
& $dotnet publish "$root\installer\Installer.csproj" -c Release -r win-x64 --nologo
if ($LASTEXITCODE -ne 0) { throw "installer publish failed" }

$setupExe = "$root\installer\bin\Release\net10.0-windows\win-x64\publish\Parrot-Setup.exe"
if (-not (Test-Path $setupExe)) { throw "setup exe not found: $setupExe" }

Write-Host "== [4/4] Collecting output to dist\ ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Force "$root\dist" | Out-Null
Copy-Item $appExe   "$root\dist\Parrot.exe" -Force
Copy-Item $setupExe "$root\dist\Parrot-Setup.exe" -Force

Write-Host ""
Write-Host "DONE" -ForegroundColor Green
Get-ChildItem "$root\dist" | ForEach-Object { "{0,-32} {1,8:N1} MB" -f $_.Name, ($_.Length/1MB) }

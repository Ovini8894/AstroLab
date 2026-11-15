#Requires -Version 5.1
<#[
.SYNOPSIS
 Diagnostico/instalacao opcional do ambiente AstroLab (Windows) para a branch nicholas.
.DESCRIPTION
 Sem -InstallTools, apenas verifica dependencias e configura editor/codigo quando possivel.
 Nunca faz push, commit, reset, stash ou alteracao na main.
.EXAMPLE
 powershell -ExecutionPolicy Bypass -File .\scripts\Setup-Nicholas-Windows.ps1
.EXAMPLE
 powershell -ExecutionPolicy Bypass -File .\scripts\Setup-Nicholas-Windows.ps1 -InstallTools
#>
param([switch]$InstallTools, [switch]$ConfigureGitIdentity)
$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$UnityRoot = Join-Path $ProjectRoot 'AstroLabUnity'
$TargetVersion = '6000.6.9f1'
Write-Host "=== AstroLab / Nicholas — Diagnostico Windows ===" -ForegroundColor Cyan

function Has-Command([string]$cmd) { return [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }
function Check-Tool([string]$cmd, [string]$wingetId) {
    if (Has-Command $cmd) {
        Write-Host "[OK] $cmd" -ForegroundColor Green
        return
    }
    Write-Warning "$cmd nao encontrado."
    if ($InstallTools -and (Has-Command winget)) {
        Write-Host "Instalando $wingetId. Reabra o terminal depois se o comando nao aparecer." -ForegroundColor Yellow
        & winget install --id $wingetId --exact --source winget --accept-package-agreements --accept-source-agreements
    } else {
        Write-Host "  Sugestao: winget install --id $wingetId --exact"
    }
}
Check-Tool 'git' 'Git.Git'
Check-Tool 'gh' 'GitHub.cli'
Check-Tool 'dotnet' 'Microsoft.DotNet.SDK.8'
Check-Tool 'code' 'Microsoft.VisualStudioCode'

if (Has-Command dotnet) {
    Write-Host 'SDKs .NET instalados:'
    & dotnet --list-sdks
    $major = (& dotnet --list-sdks | Select-String -Pattern '^(\d+)\.' | ForEach-Object { [int]$_.Matches[0].Groups[1].Value } | Measure-Object -Maximum).Maximum
    if ($major -lt 8) { Write-Warning 'Instale .NET SDK 8 ou superior.' }
}

if (Has-Command gh) {
    & gh auth status
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Login interativo necessario: execute 'gh auth login' e depois 'gh auth setup-git'." -ForegroundColor Yellow
    } else {
        & gh auth setup-git
    }
}

if ($ConfigureGitIdentity -and (Has-Command git)) {
    $who = Read-Host 'Informe o nome para git config user.name'
    $mail = Read-Host 'Informe o email para git config user.email'
    if ($who -and $mail) {
        & git -C $ProjectRoot config --local user.name "$who"
        & git -C $ProjectRoot config --local user.email "$mail"
    }
}

if (Has-Command git) {
    if (Test-Path (Join-Path $ProjectRoot '.git')) {
        $branch = & git -C $ProjectRoot branch --show-current
        Write-Host "Branch atual: $branch"
        if ($branch -ne 'nicholas') {
            Write-Warning "O repositorio NÃO está na branch nicholas. Não trocarei automaticamente se houver trabalho de colegas."
            Write-Host 'Para mudar após revisar alterações locais: git switch nicholas'
        }
        $status = & git -C $ProjectRoot status --short
        if ($status) { Write-Warning 'Há alterações locais. Revise antes de trocar de branch ou integrar arquivos.' }
    } else { Write-Warning "Nao achei .git em $ProjectRoot (zip sem historico). Para versionar, use um clone Git autêntico." }
}

$versionFile = Join-Path $UnityRoot 'ProjectSettings\ProjectVersion.txt'
if (Test-Path $versionFile) {
    $content = Get-Content $versionFile -Raw
    if ($content -match [regex]::Escape($TargetVersion)) { Write-Host "[OK] Unity no projeto: $TargetVersion" -ForegroundColor Green }
    else { Write-Warning "ProjectVersion.txt diferente de $TargetVersion" }
}
$unityExe = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$TargetVersion\Editor\Unity.exe"
if (Test-Path $unityExe) { Write-Host "[OK] Unity Editor em $unityExe" -ForegroundColor Green }
else {
    Write-Warning "Unity Editor $TargetVersion nao foi localizado no caminho padrao. Verifique a instalação pelo Hub."
    Write-Host "Unity CLI (se instalada): unity commands --grep install"
    Write-Host "Unity Hub: https://unity.com/download"
    if (-not (Has-Command unity)) { Write-Host 'Instalador da CLI indicado no brief: irm https://unity.com/install.ps1 | iex' }
}

if (Has-Command code) {
    foreach ($extension in @('ms-dotnettools.csdevkit','ms-dotnettools.csharp','visualstudiotoolsforunity.vstuc')) {
        Write-Host "Extensao VS Code: $extension"
        & code --install-extension $extension --force
    }
    Write-Host "Para abrir: code `"$UnityRoot`""
}
Write-Host 'Apos login e instalação: abra AstroLabUnity no Unity Hub (Editor EXATO 6000.6.9f1).' -ForegroundColor Cyan
Write-Host 'Unity > Edit > Preferences > External Tools > Visual Studio Code > Regenerate project files.'
Write-Host 'Unity > AstroLab > Nicholas > 1 - Criar cena de missao; depois abra MissaoNicholas e aperte Play.'
Write-Host 'Este script NAO pode validar em seu lugar Unity Play, console, screenshots e breakpoint.' -ForegroundColor Yellow

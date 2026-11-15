#Requires -Version 5.1
# Usar em uma sessão PowerShell do Windows, após instalar git e gh.
$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:USERPROFILE 'Desktop\AstroLab'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Instale Git antes de clonar.' }
if (-not (Test-Path $dest)) {
    Write-Host "Clonando para $dest ..."
    & git clone https://github.com/Ovini8894/AstroLab.git "$dest"
    if ($LASTEXITCODE -ne 0) { throw 'Clone falhou. Verifique login, URL e conexão.' }
} elseif (-not (Test-Path (Join-Path $dest '.git'))) {
    throw "$dest existe, mas não é um repositório Git. Não vou sobrescrever essa pasta."
}
Push-Location $dest
try {
    $changes = & git status --porcelain
    if ($changes) { throw 'Há alterações locais. Revise git status antes de trocar de branch.' }
    & git fetch origin
    $foundLocal = (& git branch --list nicholas).Trim()
    if ($foundLocal) {
        & git switch nicholas
    } else {
        & git show-ref --verify --quiet refs/remotes/origin/nicholas
        if ($LASTEXITCODE -eq 0) {
            & git switch --track -c nicholas origin/nicholas
        } else {
            & git show-ref --verify --quiet refs/remotes/origin/test-branch
            if ($LASTEXITCODE -eq 0) {
                & git switch -c nicholas origin/test-branch
                Write-Warning 'Branch nova criada localmente a partir de test-branch. Faça push apenas quando revisar.'
            } else {
                throw 'Não encontrei origin/nicholas nem origin/test-branch. Não vou criar branch a partir da main sem sua decisão.'
            }
        }
    }
    Write-Host "Repositório pronto na branch: $(& git branch --show-current)"
} finally { Pop-Location }

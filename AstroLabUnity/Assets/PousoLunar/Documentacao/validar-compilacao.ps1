param(
    [string]$EditorData = 'C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$validationPath = Join-Path $env:TEMP 'AstroLab-PousoLunar-Compilation'
New-Item -ItemType Directory -Path $validationPath -Force | Out-Null
$compiler = Get-ChildItem -LiteralPath (Join-Path $EditorData 'DotNetSdk/sdk') -Filter csc.dll -Recurse | Select-Object -First 1
if (!$compiler) { throw 'Compilador Roslyn do Unity não encontrado.' }
$runtime = Join-Path $EditorData 'NetCoreRuntime/dotnet.exe'
$cachedResponses = Get-ChildItem -LiteralPath (Join-Path $projectPath 'Library/Bee/artifacts') -Filter 'Assembly-CSharp-Editor.rsp' -Recurse
$cached = $cachedResponses | Select-Object -First 1
if (!$cached) { throw 'Cache de compilação ausente. Abra o Unity para gerar Library/Bee primeiro.' }
$references = Get-Content -LiteralPath $cached.FullName | Where-Object {
    $_ -match '^-r:' -and $_ -notmatch '(Assembly-CSharp|AstroLab.Menu)\.'
}
$runtimeOutput = Join-Path $validationPath 'PousoLunar.Runtime.dll'
$editorOutput = Join-Path $validationPath 'PousoLunar.Editor.dll'
Push-Location -LiteralPath $projectPath
try {
    foreach ($stage in @('Runtime','Editor')) {
        $arguments = @('-target:library','-nostdlib+','-langversion:9.0') + $references
        if ($stage -eq 'Runtime') {
            $arguments += '-out:"' + $runtimeOutput + '"'
            $arguments += Get-ChildItem -LiteralPath 'Assets/PousoLunar/Scripts' -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
            # Compila a API pública existente a partir da fonte; não altera o arquivo do Menu.
            $arguments += '"Assets/MenuInicial/Scripts/TransicaoPreta.cs"'
        } else {
            $arguments += '-define:UNITY_EDITOR'
            $arguments += '-out:"' + $editorOutput + '"'
            $arguments += '-r:"' + $runtimeOutput + '"'
            $arguments += Get-ChildItem -LiteralPath 'Assets/PousoLunar/Editor' -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
            # Paleta e componente já existentes, usados apenas pelo gerador de demonstração.
            $arguments += '"Assets/BaseLunar/Scripts/PaletaLua.cs"'
            $arguments += '"Assets/BaseLunar/Scripts/IluminacaoCartoon.cs"'
        }
        $response = Join-Path $validationPath ($stage + '.rsp')
        $arguments | Set-Content -LiteralPath $response -Encoding UTF8
        & $runtime $compiler.FullName ('@' + $response)
        if ($LASTEXITCODE -ne 0) { throw "Compilação $stage falhou." }
        Write-Output "PASS: compilação $stage. Saída: $validationPath"
    }
} finally { Pop-Location }

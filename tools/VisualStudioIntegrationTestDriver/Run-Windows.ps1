[CmdletBinding()]
param(
    [string]$Project,
    [string]$Editor = $env:SPIRE_UNITY_EDITOR,
    [string]$VisualStudio,
    [int]$TimeoutSeconds = 900,
    [switch]$NoBuild,
    [switch]$Prepared
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'This acceptance test requires an interactive Windows desktop.' }
if ($TimeoutSeconds -le 0) { throw 'TimeoutSeconds must be positive.' }
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Project) { $Project = Join-Path $root 'tmp\unity-visual-studio' }
$Project = [IO.Path]::GetFullPath($Project)
$examples = Join-Path $root 'examples'
if ($Project.TrimEnd('\', '/') -eq $examples -or $Project.StartsWith($examples + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation projects must stay outside the repository examples directory.'
}
if (-not $Editor) { $Editor = Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) { throw 'Visual Studio Installer/vswhere is missing.' }
$instances = @((& $vswhere -all -products Microsoft.VisualStudio.Product.Community -format json | ConvertFrom-Json) | Where-Object { $_.isComplete })
if ($VisualStudio) {
    $instances = @($instances | Where-Object { (Join-Path $_.installationPath 'Common7\IDE\devenv.exe') -eq [IO.Path]::GetFullPath($VisualStudio) })
}
if ($instances.Count -ne 1) { throw 'Specify one installed Visual Studio Community instance with -VisualStudio.' }
$instance = $instances[0]
$VisualStudio = Join-Path $instance.installationPath 'Common7\IDE\devenv.exe'
foreach ($path in @($Editor, $VisualStudio)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required executable is missing: $path" }
}
$env:CI = 'true'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:SPIRE_UNITY_EDITOR = [IO.Path]::GetFullPath($Editor)
$env:DOTNET_ROOT = Join-Path $env:ProgramFiles 'dotnet'
$dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'

function Quote-Argument([string]$value) {
    if ($value.Length -gt 0 -and $value -notmatch '[\s"]') { return $value }
    '"' + [regex]::Replace([regex]::Replace($value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}
function Run-Checked([string]$exe, [string[]]$argv, [int]$seconds, [string]$log, [string]$directory = $root) {
    $arguments = ($argv | ForEach-Object { Quote-Argument $_ }) -join ' '
    $p = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $directory -PassThru -RedirectStandardOutput ($log + '.stdout.log') -RedirectStandardError ($log + '.stderr.log')
    $null = $p.Handle
    if (-not $p.WaitForExit($seconds * 1000)) {
        & (Join-Path $env:SystemRoot 'System32\taskkill.exe') /PID $p.Id /T /F | Out-Null
        throw "Timed out; inspect $log"
    }
    $p.Refresh()
    if ($p.ExitCode -ne 0) { throw "Exit $($p.ExitCode); inspect $log" }
}

if (-not $Prepared) {
    $cliProject = Join-Path $root 'tools\Spire.Tool\Spire.Tool.csproj'
    if (-not $NoBuild) {
        & $dotnet build $cliProject -c Release --nologo -p:UseSharedCompilation=false -p:EnableSourceControlManagerQueries=false
        if ($LASTEXITCODE -ne 0) { throw 'Could not build Spire.Tool.' }
    }
    $cli = Join-Path $root 'tools\Spire.Tool\bin\Release\net10.0\Spire.Tool.dll'
    $arguments = @($cli, 'unity-asmdefs', '--project', $Project, '--editor', $Editor, '--visual-studio', $VisualStudio, '--timeout', [string]$TimeoutSeconds)
    if ($NoBuild) { $arguments += '--no-build' }
    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Unity asmdef acceptance failed.' }
}
$marker = Join-Path $Project '.spire-unity-asmdefs'
if (-not (Test-Path -LiteralPath $marker) -or [IO.File]::ReadAllText($marker) -ne "Spire assembly definition integration`n") {
    throw 'Use the disposable project prepared by unity-asmdefs.'
}
$state = Get-Content -LiteralPath (Join-Path $Project 'asmdef-results.json') -Raw | ConvertFrom-Json
if ($state.status -ne 'pass' -or $state.projectGenerator -ne 'VisualStudio') { throw 'A passing Visual Studio Unity project generation report is required.' }
$solution = [IO.Path]::GetFullPath($state.solutionPath)
if ((Split-Path $solution -Parent) -ne $Project -or -not (Test-Path -LiteralPath $solution -PathType Leaf)) { throw 'The generated solution must belong to the disposable project.' }
$source = Join-Path $Project 'Assets\RuntimeAuto\_rider.cs'
$before = [IO.File]::ReadAllText($source)
if ([regex]::Matches($before, '\(ProbeKind\)value').Count -ne 1) { throw 'Prepare a fresh fixture with exactly one unfixed enum cast.' }
$expected = $before.Replace('(ProbeKind)value', 'SpireEnum<ProbeKind>.From(value)')
$work = Join-Path $Project 'VisualStudioDriver'
New-Item -ItemType Directory -Path $work -Force | Out-Null
$major = ([version]$instance.installationVersion).Major
$suffix = 'SpireValidation'
$profile = Join-Path $env:LOCALAPPDATA ("Microsoft\VisualStudio\$major.0_" + $instance.instanceId + $suffix)
if (Get-CimInstance Win32_Process -Filter "Name='devenv.exe'" | Where-Object { $_.CommandLine -match '/RootSuffix\s+SpireValidation\b' }) {
    throw 'Close the existing SpireValidation Visual Studio instance before this run.'
}
if (-not $NoBuild) {
    & $dotnet build (Join-Path $PSScriptRoot 'VisualStudioIntegrationTestDriver.csproj') -c Release --nologo -p:EnableSourceControlManagerQueries=false
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the IDE test driver.' }
}
$dll = Join-Path $PSScriptRoot 'bin\Release\net472\VisualStudioIntegrationTestDriver.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Build the IDE test driver first.' }
if (-not (Test-Path -LiteralPath $profile)) {
    $create = Get-ChildItem -LiteralPath $instance.installationPath -Filter CreateExpInstance.exe -Recurse -File | Select-Object -First 1 -ExpandProperty FullName
    if (-not $create) { throw 'Install the Visual Studio extension development workload.' }
    Run-Checked $create @('/Create', "/VSInstance=$major.0", "/RootSuffix=$suffix") 180 (Join-Path $work 'create-profile')
}
$installed = @(Get-ChildItem -LiteralPath (Join-Path $profile 'Extensions') -Filter VisualStudioIntegrationTestDriver.dll -Recurse -File -ErrorAction SilentlyContinue)
if ($installed.Count -gt 1) { throw 'Multiple private driver installations were found.' }
if ($installed.Count -eq 1) {
    Copy-Item -LiteralPath $dll -Destination $installed[0].FullName -Force
} else {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $staging = Join-Path $work 'vsix'
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Copy-Item -LiteralPath $dll -Destination $staging -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'extension.vsixmanifest') -Destination $staging -Force
    [IO.File]::WriteAllText((Join-Path $staging '[Content_Types].xml'), '<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="dll" ContentType="application/octet-stream"/><Default Extension="vsixmanifest" ContentType="text/xml"/></Types>')
    $vsix = Join-Path $work 'SpireIntegrationDriver.vsix'
    if (Test-Path -LiteralPath $vsix) { Remove-Item -LiteralPath $vsix -Force }
    [IO.Compression.ZipFile]::CreateFromDirectory($staging, $vsix)
    Run-Checked (Join-Path $instance.installationPath 'Common7\IDE\VSIXInstaller.exe') @('/quiet', "/rootSuffix:$suffix", ("/instanceIds:" + $instance.instanceId), $vsix) 180 (Join-Path $work 'install-driver')
}
$cache = Join-Path $profile 'ComponentModelCache'
if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Recurse -Force }
Run-Checked $VisualStudio @('/RootSuffix', $suffix, '/UpdateConfiguration') 180 (Join-Path $work 'configure-profile')
$report = Join-Path $work 'ide-results.json'
if (Test-Path -LiteralPath $report) { Move-Item -LiteralPath $report -Destination ($report + '.' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '.previous') }
$env:SPIRE_VS_TEST_PROJECT = $Project
$env:SPIRE_VS_TEST_REPORT = $report
if (-not ('SpireValidation.VisualStudioAutomation' -as [type])) {
    Add-Type -LiteralPath (Join-Path $PSScriptRoot 'VisualStudioAutomation.cs')
}
$ideArguments = (@($solution, '/RootSuffix', $suffix, '/Log', (Join-Path $work 'ActivityLog.xml'), '/NoSplash') | ForEach-Object { Quote-Argument $_ }) -join ' '
$ide = Start-Process -FilePath $VisualStudio -ArgumentList $ideArguments -WorkingDirectory $Project -PassThru
$null = $ide.Handle
$clock = [Diagnostics.Stopwatch]::StartNew()
$opened = $false
$automationError = ''
try {
    while (-not $ide.HasExited -and $clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if (-not $opened) {
            $dte = $null
            try {
                $dte = [SpireValidation.VisualStudioAutomation]::TryGetDte($ide.Id)
                if ($dte -and [string]::Equals($dte.Solution.FullName, $solution, [StringComparison]::OrdinalIgnoreCase)) {
                    $null = $dte.ItemOperations.OpenFile($source)
                    $opened = $true
                }
            } catch { $automationError = $_.Exception.Message }
            finally { if ($dte) { $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($dte) } }
        }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ide.HasExited) { throw "IDE timed out; source opened=$opened; automation=$automationError" }
    $ide.Refresh()
    if ($ide.ExitCode -ne 0 -or -not $opened) { throw "IDE startup failed; automation=$automationError" }
} finally {
    if (-not $ide.HasExited) { & (Join-Path $env:SystemRoot 'System32\taskkill.exe') /PID $ide.Id /T /F | Out-Null }
    $ide.Dispose()
}
$result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if ($result.status -ne 'pass' -or -not $result.discovered -or -not $result.applied -or -not $result.generatedSymbols -or -not $result.diagnosticsRefreshed) { throw "IDE acceptance failed: $report" }
if ([IO.File]::ReadAllText($source) -cne $expected) { throw 'The persisted IDE transformation does not match the fixture.' }
$recompile = Join-Path $Project 'visual-studio-recompile-results.json'
if (Test-Path -LiteralPath $recompile) { Remove-Item -LiteralPath $recompile -Force }
Run-Checked $Editor @('-batchmode', '-nographics', '-projectPath', $Project, '-executeMethod', 'Spire.UnityAsmdefIntegration.AsmdefIntegration.VerifyVisualStudioFix', '-logFile', (Join-Path $Project 'visual-studio-recompile.log')) $TimeoutSeconds (Join-Path $work 'unity-recompile')
$unity = Get-Content -LiteralPath $recompile -Raw | ConvertFrom-Json
if ($unity.status -ne 'pass') { throw "Unity recompilation failed: $recompile" }
Write-Output "Visual Studio and Unity acceptance passed: $report"

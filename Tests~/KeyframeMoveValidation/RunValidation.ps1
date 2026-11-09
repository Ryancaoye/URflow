param(
    [ValidateSet('Bootstrap', 'Service', 'UiKeys', 'UiActual', 'UiStrict')]
    [string]$Suite = 'Bootstrap',
    [string]$UnityPath = 'G:\L50_1_1_1\UnityEditor\Editor\Unity.exe',
    [string]$ProjectPath = 'D:\插件\URFLOW\6-开发\测试验证\v1.0.4'
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity editor is unavailable: $UnityPath" }
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Assets\Editor') -PathType Container)) { throw "Copy Editor sources into the isolated project's Assets\Editor first." }
if ($UnityPath.Contains('"') -or $ProjectPath.Contains('"')) { throw 'Paths must not contain quote characters.' }
$urValidationMethods = @{
    Bootstrap = 'URFlowValidationBootstrap.CompileOnly'
    Service = 'URFlowServiceChecks.Run'
    UiKeys = 'URFlowUiProbeRunner.RunKeys'
    UiActual = 'URFlowUiProbeRunner.RunActual'
    UiStrict = 'URFlowUiProbeRunner.Run'
}
$urValidationReports = @{
    Bootstrap = 'bootstrap-result.txt'
    Service = 'service-result.txt'
    UiKeys = 'ui-key-interactions-result.txt'
    UiActual = 'ui-actual-native-result.txt'
    UiStrict = 'ui-event-probe-result.txt'
}
$urValidationLogDirectory = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Path $urValidationLogDirectory -Force | Out-Null
$urValidationLogPath = Join-Path $urValidationLogDirectory ($Suite.ToLowerInvariant() + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$urValidationArguments = @('-projectPath', ('"' + $ProjectPath + '"'), '-executeMethod', $urValidationMethods[$Suite], '-logFile', ('"' + $urValidationLogPath + '"'))
if ($Suite -eq 'Bootstrap') { $urValidationArguments = @('-batchmode', '-nographics') + $urValidationArguments }
# Service/UI require an actual nonbatch editor and its native AnimationWindow/IMGUI lifecycle.
# Each runner exits only after its scheduled editor checks finish; do not add -quit.
$urValidationProcess = Start-Process -FilePath $UnityPath -ArgumentList $urValidationArguments -WorkingDirectory $ProjectPath -WindowStyle Hidden -PassThru
[pscustomobject]@{
    ProcessId = $urValidationProcess.Id
    Suite = $Suite
    Project = $ProjectPath
    BatchMode = ($Suite -eq 'Bootstrap')
    Log = $urValidationLogPath
    Report = (Join-Path $urValidationLogDirectory $urValidationReports[$Suite])
}

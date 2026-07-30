param(
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$ResultsDirectory = "TestResults"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe"
}

$resolvedProject = [IO.Path]::GetFullPath($ProjectPath)
$resolvedUnity = [IO.Path]::GetFullPath($UnityPath)
$resolvedResults = if ([IO.Path]::IsPathRooted($ResultsDirectory)) {
    [IO.Path]::GetFullPath($ResultsDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path $resolvedProject $ResultsDirectory))
}

if (!(Test-Path -LiteralPath $resolvedUnity -PathType Leaf)) {
    throw "Unity Editor was not found at '$resolvedUnity'. Set -UnityPath or UNITY_EDITOR_PATH."
}

New-Item -ItemType Directory -Path $resolvedResults -Force | Out-Null

function Invoke-UnityTestPlatform {
    param([ValidateSet("EditMode", "PlayMode")][string]$Platform)

    $name = $Platform.ToLowerInvariant()
    $resultPath = Join-Path $resolvedResults "$name-results.xml"
    $logPath = Join-Path $resolvedResults "$name-editor.log"
    $arguments = @(
        "-batchmode",
        "-projectPath", $resolvedProject,
        "-runTests",
        "-testPlatform", $Platform,
        "-testResults", $resultPath,
        "-logFile", $logPath
    )

    $process = Start-Process -FilePath $resolvedUnity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) {
        throw "$Platform Unity tests failed with exit code $($process.ExitCode). See '$logPath'."
    }
    if (!(Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "$Platform Unity tests did not create '$resultPath'."
    }

    [xml]$result = Get-Content -LiteralPath $resultPath
    $run = $result.'test-run'
    if ($null -eq $run -or $run.result -ne "Passed") {
        throw "$Platform Unity result was '$($run.result)'. See '$resultPath' and '$logPath'."
    }

    Write-Output "$Platform passed: total=$($run.total) passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped)"
}

Invoke-UnityTestPlatform -Platform EditMode
Invoke-UnityTestPlatform -Platform PlayMode

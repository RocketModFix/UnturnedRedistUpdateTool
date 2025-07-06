#
# Finds all tests projects for a project passed as the first argument
# and runs a command passed as the second argument for every tests project found.
# Also sets PROJECT_PATH environment variables with value of the tests project folder path.
#
# Example usage:
# pwsh -f .github/actions/project-build/run-command-for-every-tests-project.ps1 "framework/OpenMod.Core" "echo \$PROJECT_PATH"
#
# Example output:
# Tests project found: framework/OpenMod.Core/../tests/OpenMod.Core.Tests. Executing a command: echo $PROJECT_PATH
# framework/OpenMod.Core/../tests/OpenMod.Core.Tests

$projectPath = $args[0]
$projectName = Split-Path -Path $projectPath -Leaf
$testsFolderPath = Join-Path -Path $PSScriptRoot -ChildPath "../../../tests"
$global:exitCode = 0
$commandToExecute = $args[1]
$allowNoTests = $args.Count -ge 3 -and $args[2] -eq "--allow-no-tests"

Write-Host "Looking for tests in: $testsFolderPath"

$testsFound = @(
    Get-ChildItem -Path $testsFolderPath -Directory -Recurse |
    Where-Object { $_.Name -match "^$projectName.*Tests$" }
)

if ($testsFound.Count -eq 0) {
    if ($allowNoTests) {
        Write-Host "No test projects found for project '$projectName', but skipping due to --allow-no-tests flag."
        exit 0
    }
    else {
        Write-Host "No test projects found for project '$projectName'."
        exit 1
    }
}

foreach ($testProject in $testsFound) {
    $testsProjectName = $testProject.Name
    $testsProjectPath = Join-Path -Path $testsFolderPath -ChildPath $testsProjectName
    Write-Host "Tests project found: $testsProjectPath. Executing a command: $commandToExecute"

    try {
        bash -c "PROJECT_PATH=$testsProjectPath && $commandToExecute"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Command failed with exit code $LASTEXITCODE"
            $global:exitCode = $LASTEXITCODE
        }
    }
    catch {
        Write-Host "Exception while running command in: $testsProjectPath"
        $global:exitCode = 1
    }
}

if ($global:exitCode -ne 0) {
    Write-Host "`nOne or more test commands failed."
    exit $global:exitCode
}
else {
    Write-Host "`nAll test projects executed successfully."
}
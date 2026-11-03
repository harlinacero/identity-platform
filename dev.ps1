# Project commands for Windows. Requires only Docker Desktop (no Make, no SDKs).
# Mirrors the Makefile; both delegate to Docker Compose. See ADR-0005.
Set-Location $PSScriptRoot -ErrorAction Stop

$composeArgs = @(
    'compose', '--project-directory', '.',
    '-f', 'deploy/compose/docker-compose.infra.yml',
    '-f', 'deploy/compose/docker-compose.services.yml',
    '-f', 'deploy/compose/docker-compose.tools.yml'
)

function Invoke-Docker {
    & docker @args
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Invoke-Compose {
    Invoke-Docker @composeArgs @args
}

function Invoke-Tool {
    param([string]$Tool, [string]$Dir, [string[]]$Command)
    Invoke-Compose run --rm -w "/workspace/$($Dir -replace '\\', '/')" $Tool @Command
}

# Keep in sync with the per-stack targets in the Makefile.
$projects = @(
    @{ Stack = 'dotnet'; Tool = 'dotnet'; Dirs = 'services/gateway', 'services/identity', 'services/tenants-users'; Manifest = '*.sln*'
        Lint = 'dotnet format --verify-no-changes'; Build = 'dotnet build'; Test = 'dotnet test' }
    @{ Stack = 'java'; Tool = 'java'; Dirs = @('services/ldap-connector'); Manifest = 'gradlew'
        Lint = './gradlew check -x test'; Build = './gradlew assemble'; Test = './gradlew test' }
    @{ Stack = 'go'; Tool = 'go'; Dirs = @('services/audit'); Manifest = 'go.mod'
        Lint = 'go vet ./... && gofmt -l . | (! grep .)'; Build = 'go build ./...'; Test = 'go test ./...' }
    @{ Stack = 'python'; Tool = 'python'; Dirs = @('services/risk'); Manifest = 'pyproject.toml'
        Lint = 'pip install -q -e .[dev] && ruff check . && mypy .'; Build = 'pip install -q .'; Test = 'pip install -q -e .[dev] && pytest' }
    @{ Stack = 'node'; Tool = 'node'; Dirs = @('services/notifications'); Manifest = 'package.json'
        Lint = 'npm ci && npm run lint'; Build = 'npm ci && npm run build'; Test = 'npm ci && npm test' }
    @{ Stack = 'frontends'; Tool = 'node'; Dirs = 'frontends/account-web', 'frontends/admin-console'; Manifest = 'package.json'
        Lint = 'npm ci && npm run lint'; Build = 'npm ci && npm run build'; Test = 'npm ci && npm test' }
)

function Invoke-ProjectCheck {
    param([string]$Check, [string]$Stack)
    $selected = @($projects | Where-Object { -not $Stack -or $_.Stack -eq $Stack })
    if ($selected.Count -eq 0) { Show-Usage; exit 1 }
    foreach ($project in $selected) {
        foreach ($dir in $project.Dirs) {
            if (Test-Path "$dir/$($project.Manifest)") {
                Write-Host "==> ${dir}: $($project[$Check])"
                Invoke-Compose run --rm -T -w "/workspace/$dir" $project.Tool sh -c $project[$Check]
            } else {
                Write-Host "--> ${dir}: no project yet, skipping"
            }
        }
    }
}

function Show-Usage {
    @'
Usage: ./dev.ps1 <command>

  infra                       Start infrastructure and wait for healthchecks
  up                          Start infrastructure and services
  down                        Stop everything (keeps data volumes)
  ps                          Show container status
  logs [service]              Follow logs
  lint [stack]                Validate Compose and lint projects
  build [stack]               Build projects
  test [stack]                Test projects
  run <tool> <dir> <cmd...>   Run a command in a toolchain container
  shell <tool>                Open a shell in a toolchain container
  k8s-up                      Create the local kind cluster
  k8s-down                    Delete the local kind cluster
  load-test                   Run the k6 sign-in load test

Tools: dotnet, java, go, python, node, k6, terraform, k8s
Stacks: dotnet, java, go, python, node, frontends

Examples:
  ./dev.ps1 run dotnet services/identity dotnet test
  ./dev.ps1 run java services/ldap-connector ./gradlew test
  ./dev.ps1 run go services/audit go test ./...
  ./dev.ps1 run python services/risk pytest
  ./dev.ps1 run node services/notifications npm test
'@
}

if (-not (Test-Path .env)) {
    Copy-Item .env.example .env -ErrorAction Stop
    Write-Host 'Created .env from .env.example'
}

$command = $args | Select-Object -First 1
$rest = @($args | Select-Object -Skip 1)
switch ($command) {
    'infra' { Invoke-Compose up -d --wait postgres redis kafka rabbitmq opensearch openldap mailpit }
    'up' { Invoke-Compose up -d --build --wait }
    'down' { Invoke-Compose down }
    'ps' { Invoke-Compose ps -a }
    'logs' { Invoke-Compose logs -f @rest }
    'lint' {
        if (-not $rest) { Invoke-Compose config --quiet }
        Invoke-ProjectCheck -Check Lint -Stack ($rest | Select-Object -First 1)
    }
    'build' { Invoke-ProjectCheck -Check Build -Stack ($rest | Select-Object -First 1) }
    'test' { Invoke-ProjectCheck -Check Test -Stack ($rest | Select-Object -First 1) }
    'run' {
        if ($rest.Count -lt 3) { Show-Usage; exit 1 }
        Invoke-Tool -Tool $rest[0] -Dir $rest[1] -Command @($rest | Select-Object -Skip 2)
    }
    'shell' {
        if ($rest.Count -ne 1) { Show-Usage; exit 1 }
        $shell = if ($rest[0] -in 'k6', 'terraform', 'k8s') { 'sh' } else { 'bash' }
        Invoke-Tool -Tool $rest[0] -Dir '.' -Command @($shell)
    }
    'k8s-up' {
        Invoke-Tool -Tool k8s -Dir '.' -Command @('kind', 'create', 'cluster', '--config', 'deploy/k8s/kind-config.yaml', '--wait', '3m')
    }
    'k8s-down' {
        Invoke-Tool -Tool k8s -Dir '.' -Command @('kind', 'delete', 'cluster', '--name', 'identity-platform')
    }
    'load-test' { Invoke-Tool -Tool k6 -Dir '.' -Command @('k6', 'run', 'tests/load/login.js') }
    default { Show-Usage; if ($command) { exit 1 } }
}

# Project commands for systems with Make. On Windows, ./dev.ps1 offers the same commands.
COMPOSE = docker compose --project-directory . \
	-f deploy/compose/docker-compose.infra.yml \
	-f deploy/compose/docker-compose.services.yml \
	-f deploy/compose/docker-compose.tools.yml
DIR ?= .
FOLLOW ?= -f

DOTNET_DIRS = services/gateway services/identity services/tenants-users
JAVA_DIRS = services/ldap-connector
GO_DIRS = services/audit
PYTHON_DIRS = services/risk
NODE_DIRS = services/notifications
FRONTEND_DIRS = frontends/account-web frontends/admin-console

# Runs a command in a toolchain container for each project that has its manifest.
# Arguments: tool, directories, manifest, command.
define for-each-project
@for dir in $(2); do \
	if ls $$dir/$(3) >/dev/null 2>&1; then \
		echo "==> $$dir: $(4)"; \
		$(COMPOSE) run --rm -T -w /workspace/$$dir $(1) sh -c '$(4)' || exit 1; \
	else \
		echo "--> $$dir: no project yet, skipping"; \
	fi; \
done
endef

.DEFAULT_GOAL := help

help:
	@echo "Usage: make <command>"
	@echo ""
	@echo "  infra                        Start infrastructure and wait for healthchecks"
	@echo "  up                           Start infrastructure and services"
	@echo "  down                         Stop everything (keeps data volumes)"
	@echo "  ps                           Show container status"
	@echo "  logs [SERVICE=name]          Follow logs"
	@echo "  lint                         Validate Compose and lint every project"
	@echo "  build                        Build every project"
	@echo "  test                         Test every project"
	@echo "  run TOOL= DIR= CMD=          Run a command in a toolchain container"
	@echo "  shell TOOL=                  Open a shell in a toolchain container"
	@echo "  k8s-up                       Create the local kind cluster"
	@echo "  k8s-down                     Delete the local kind cluster"
	@echo "  load-test                    Run the k6 sign-in load test"
	@echo ""
	@echo "lint, build and test also exist per stack, for example make lint-dotnet."
	@echo "Stacks: dotnet, java, go, python, node, frontends"

.env:
	cp .env.example .env

infra: .env
	$(COMPOSE) up -d --wait postgres redis kafka rabbitmq opensearch openldap mailpit

up: .env
	$(COMPOSE) up -d --build --wait

down: .env
	$(COMPOSE) down

ps: .env
	$(COMPOSE) ps -a

logs: .env
	$(COMPOSE) logs $(FOLLOW) $(SERVICE)

lint: lint-compose lint-dotnet lint-java lint-go lint-python lint-node lint-frontends

build: build-dotnet build-java build-go build-python build-node build-frontends

test: test-dotnet test-java test-go test-python test-node test-frontends

lint-compose: .env
	$(COMPOSE) config --quiet

lint-dotnet: .env
	$(call for-each-project,dotnet,$(DOTNET_DIRS),*.sln*,dotnet format --verify-no-changes)

build-dotnet: .env
	$(call for-each-project,dotnet,$(DOTNET_DIRS),*.sln*,dotnet build)

test-dotnet: .env
	$(call for-each-project,dotnet,$(DOTNET_DIRS),*.sln*,dotnet test)

lint-java: .env
	$(call for-each-project,java,$(JAVA_DIRS),gradlew,./gradlew check -x test)

build-java: .env
	$(call for-each-project,java,$(JAVA_DIRS),gradlew,./gradlew assemble)

test-java: .env
	$(call for-each-project,java,$(JAVA_DIRS),gradlew,./gradlew test)

lint-go: .env
	$(call for-each-project,go,$(GO_DIRS),go.mod,go vet ./... && gofmt -l . | (! grep .))

build-go: .env
	$(call for-each-project,go,$(GO_DIRS),go.mod,go build ./...)

test-go: .env
	$(call for-each-project,go,$(GO_DIRS),go.mod,go test ./...)

lint-python: .env
	$(call for-each-project,python,$(PYTHON_DIRS),pyproject.toml,pip install -q -e .[dev] && ruff check . && mypy .)

build-python: .env
	$(call for-each-project,python,$(PYTHON_DIRS),pyproject.toml,pip install -q .)

test-python: .env
	$(call for-each-project,python,$(PYTHON_DIRS),pyproject.toml,pip install -q -e .[dev] && pytest)

lint-node: .env
	$(call for-each-project,node,$(NODE_DIRS),package.json,npm ci && npm run lint)

build-node: .env
	$(call for-each-project,node,$(NODE_DIRS),package.json,npm ci && npm run build)

test-node: .env
	$(call for-each-project,node,$(NODE_DIRS),package.json,npm ci && npm test)

lint-frontends: .env
	$(call for-each-project,node,$(FRONTEND_DIRS),package.json,npm ci && npm run lint)

build-frontends: .env
	$(call for-each-project,node,$(FRONTEND_DIRS),package.json,npm ci && npm run build)

test-frontends: .env
	$(call for-each-project,node,$(FRONTEND_DIRS),package.json,npm ci && npm test)

# Example: make run TOOL=dotnet DIR=services/identity CMD="dotnet test"
run: .env
	$(COMPOSE) run --rm -w /workspace/$(DIR) $(TOOL) $(CMD)

# Example: make shell TOOL=go
shell: .env
	$(COMPOSE) run --rm $(TOOL) $(if $(filter k6 terraform k8s,$(TOOL)),sh,bash)

k8s-up: .env
	$(COMPOSE) run --rm k8s kind create cluster --config deploy/k8s/kind-config.yaml --wait 3m

k8s-down: .env
	$(COMPOSE) run --rm k8s kind delete cluster --name identity-platform

load-test: .env
	$(COMPOSE) run --rm k6 k6 run tests/load/login.js

.PHONY: help infra up down ps logs lint build test run shell k8s-up k8s-down load-test lint-compose \
	lint-dotnet build-dotnet test-dotnet lint-java build-java test-java lint-go build-go test-go \
	lint-python build-python test-python lint-node build-node test-node \
	lint-frontends build-frontends test-frontends

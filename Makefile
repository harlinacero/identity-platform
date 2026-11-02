# Project commands for systems with Make. On Windows, ./dev.ps1 offers the same commands.
COMPOSE = docker compose --project-directory . \
	-f deploy/compose/docker-compose.infra.yml \
	-f deploy/compose/docker-compose.services.yml \
	-f deploy/compose/docker-compose.tools.yml
DIR ?= .

.env:
	cp .env.example .env

infra: .env
	$(COMPOSE) up -d --wait postgres redis kafka rabbitmq opensearch openldap mailpit

up: .env
	$(COMPOSE) up -d --build --wait

down: .env
	$(COMPOSE) down

# Example: make run TOOL=dotnet DIR=services/identity CMD="dotnet test"
run: .env
	$(COMPOSE) run --rm -w /workspace/$(DIR) $(TOOL) $(CMD)

k8s-up: .env
	$(COMPOSE) run --rm k8s kind create cluster --config deploy/k8s/kind-config.yaml --wait 3m

k8s-down: .env
	$(COMPOSE) run --rm k8s kind delete cluster --name identity-platform

load-test: .env
	$(COMPOSE) run --rm k6 k6 run tests/load/login.js

.PHONY: infra up down run k8s-up k8s-down load-test

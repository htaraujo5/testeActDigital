# Conta Empresarial — Account Manager

Solução .NET 8 para controle de movimentações de uma **conta empresarial**: entradas (crédito), saídas (débito), consulta de **saldo** e **histórico**, com garantia de que o **saldo nunca fica negativo**.

**Desafio técnico:** act digital — Engenheiro de Software .NET  

**Repositório:** _(cole aqui a URL do GitHub público após criar o remote)_

---

## Sumário

1. [Escopo do desafio](#1-escopo-do-desafio)
2. [O que foi desenvolvido](#2-o-que-foi-desenvolvido)
3. [Arquitetura](#3-arquitetura)
4. [Como executar](#4-como-executar)
5. [API e autenticação](#5-api-e-autenticação)
6. [Testes](#6-testes)
7. [Decisões técnicas](#7-decisões-técnicas)
8. [Evoluções futuras](#8-evoluções-futuras)

---

## 1. Escopo do desafio

### Requisitos de negócio

| Requisito | Status | Implementação |
|-----------|:------:|---------------|
| Registrar entradas (crédito) | OK | `POST /api/v1/accounts/{id}/credits` |
| Registrar saídas (débito) | OK | `POST /api/v1/accounts/{id}/debits` |
| Consultar saldo disponível | OK | `GET /api/v1/accounts/{id}/balance` |
| Consultar histórico | OK | `GET /api/v1/accounts/{id}/transactions` |
| Saldo nunca negativo | OK | Domínio + `UPDATE` condicional + `CHECK` no Postgres |

### Entrega esperada

| Item | Status |
|------|:------:|
| API C# / .NET | OK |
| Testes | OK |
| Boas práticas de desenvolvimento | OK |
| Explicação da estrutura | OK (este README) |
| README com instruções de execução | OK |
| Documentação no projeto | OK |
| Repositório público no GitHub | A publicar |
| **Diferencial:** interface React | OK |

### Critérios de avaliação

| Critério | Como foi atendido |
|----------|-------------------|
| Funcionamento | Fluxo completo validado em testes de integração e Docker |
| Clareza e organização | Clean Architecture + CQRS + Controllers |
| Qualidade dos testes | Unitários (domínio/application) + integração (Postgres/Redis) |
| Coerência técnica | Redis só coordena; saldo só no banco; leitura na réplica |
| Capacidade de explicar/alterar | Decisões e evoluções documentadas abaixo |

O enunciado pede evitar complexidade desnecessária. Itens além do mínimo (gateway, réplica, Redis, métricas) estão justificados na seção [Decisões técnicas](#7-decisões-técnicas) e podem ser simplificados se a avaliação preferir um núcleo menor.

---

## 2. O que foi desenvolvido

### Núcleo financeiro
- Conta criada sob demanda na primeira mutação
- Crédito e débito atômicos (ledger + saldo + auditoria na mesma transação)
- Débito com fundos insuficientes → `422`, saldo inalterado
- Histórico ordenado por data
- Idempotência com header `Idempotency-Key` (Redis + UNIQUE no banco)

### Backend
- Clean Architecture: Domain → Application → Infrastructure → Api
- CQRS com `ICommandHandler` / `IQueryHandler` (sem MediatR)
- EF Core **somente** na escrita; Dapper **somente** na leitura
- Repository pattern; models EF separados das entities de domínio
- Controllers (sem Minimal API de negócio)

### Gateway BFF
- Controllers + DTOs próprios + AutoMapper
- Load balance RoundRobin (`api-1` / `api-2`) via `HttpClient`
- Header de resposta `X-Api-Instance` para evidenciar o nó

### Infraestrutura
- Postgres primary (write) + streaming replica (read) com fallback
- Redis: lock, idempotência, gate de saúde da réplica
- Circuit breakers Polly (`db_write`, `db_read`, `redis`)
- JWT (`admin` / `operator`)
- Audit append-only (sucesso e rejeição)
- Serilog + Prometheus + Grafana

### Front (diferencial)
- React + Vite + TypeScript — UI **Conta Digital**
- Login, saldo em destaque, entrada/saída (modal), histórico, toasts, mobile

---

## 3. Arquitetura

```text
                         ┌─────────────────┐
                         │  React UI :3000 │
                         │  Swagger / HTTP │
                         └────────┬────────┘
                                  │
                         ┌────────▼────────┐
                         │  Gateway BFF    │
                         │  Controllers +  │
                         │  AutoMapper +   │
                         │  RoundRobin     │
                         │  :8080          │
                         └────────┬────────┘
                                  │
                 ┌────────────────┼────────────────┐
                 ▼                                 ▼
        ┌────────────────┐                ┌────────────────┐
        │    api-1       │                │    api-2       │
        │  Controllers   │                │  Controllers   │
        └───────┬────────┘                └───────┬────────┘
                │                                 │
                └────────────┬────────────────────┘
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
     ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
     │  Commands   │  │   Queries   │  │    Redis    │
     │  (escrita)  │  │  (leitura)  │  │  lock/idem  │
     └──────┬──────┘  └──────┬──────┘  │  lag gate   │
            │                │         └─────────────┘
            │                │
            ▼                ▼
   ┌────────────────┐  ┌────────────────┐
   │ Postgres Write │  │ Postgres Read  │
   │   (primary)    │─►│   (replica)    │
   │  EF Core       │  │   Dapper       │
   └────────────────┘  └────────────────┘
            │
            │  /metrics
            ▼
   ┌────────────────┐      ┌────────────────┐
   │  Prometheus    │─────►│    Grafana     │
   │  :9090         │      │    :3001       │
   └────────────────┘      └────────────────┘
```

### Fluxo

1. Cliente (UI ou Swagger) → **Gateway BFF** (`:8080`)
2. Gateway mapeia DTOs e distribui entre `api-1` / `api-2` (RoundRobin)
3. **Commands** → Redis (lock/idem) → **Postgres Write** + audit
4. **Queries** → gate Redis → **Postgres Read** (Dapper); se réplica indisponível → Write
5. Prometheus coleta `/metrics`; Grafana exibe o dashboard

### Estrutura de pastas

```text
src/
  AccountManager.Domain/           Entities, Enums, DomainResult
  AccountManager.Application/      Commands, Queries, Interfaces, Services
  AccountManager.Infrastructure/   EF Write, Dapper Read, Redis, JWT, Polly
  AccountManager.Api/              Controllers HTTP da API
  AccountManager.Gateway/          BFF (Controllers, DTOs, AutoMapper, LB)
web/                               Front Conta Digital (React + Vite)
tests/
  AccountManager.Domain.Tests/
  AccountManager.Application.Tests/
  AccountManager.Integration.Tests/
docker/                            Replica Postgres, Prometheus, Grafana
```

---

## 4. Como executar

### Pré-requisito
Docker Desktop em execução.

### Subir a stack

```bash
docker compose up --build
```

| Serviço | URL / porta |
|---------|-------------|
| UI Conta Digital | http://localhost:3000 |
| Gateway + Swagger BFF | http://localhost:8080 · `/swagger` |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3001 (`admin` / `admin`) |
| Postgres write / read | `5432` / `5433` |
| Redis | `6379` |

Para atualizar só o front:

```bash
docker compose up --build -d web
```

### Desenvolvimento local (API)

```bash
dotnet restore AccountManager.slnx
dotnet run --project src/AccountManager.Api
```

Configure `ConnectionStrings` em `src/AccountManager.Api/appsettings.json` (`WriteDb`, `ReadDb`, `Redis`).

---

## 5. API e autenticação

### Login

```http
POST /api/v1/auth/token
Content-Type: application/json

{ "username": "admin", "password": "Admin@123" }
```

| Usuário | Senha | Role |
|---------|-------|------|
| `admin` | `Admin@123` | Admin |
| `operator` | `Operator@123` | Operator |

### Headers

| Header | Uso |
|--------|-----|
| `Authorization: Bearer {token}` | Obrigatório nas rotas de conta |
| `Idempotency-Key` | Obrigatório em POST de crédito/débito |
| `X-Correlation-Id` | Opcional (gerado se ausente) |

### Endpoints principais

```http
POST /api/v1/accounts/{accountId}/credits
POST /api/v1/accounts/{accountId}/debits
GET  /api/v1/accounts/{accountId}/balance
GET  /api/v1/accounts/{accountId}/transactions?take=50
GET  /health/live
GET  /health/ready
GET  /meta/circuits   # Admin
GET  /metrics
```

### Exemplo (PowerShell)

```powershell
$token = (Invoke-RestMethod -Method Post -Uri "http://localhost:8080/api/v1/auth/token" `
  -ContentType "application/json" `
  -Body '{ "username":"admin", "password":"Admin@123" }').accessToken

$accountId = [guid]::NewGuid()

Invoke-RestMethod -Method Post `
  -Uri "http://localhost:8080/api/v1/accounts/$accountId/credits" `
  -Headers @{ "Authorization"="Bearer $token"; "Idempotency-Key"="c1" } `
  -ContentType "application/json" `
  -Body '{ "amount": 100 }'

Invoke-RestMethod `
  -Uri "http://localhost:8080/api/v1/accounts/$accountId/balance" `
  -Headers @{ "Authorization"="Bearer $token" }
```

---

## 6. Testes

| Projeto | Tipo | Foco |
|---------|------|------|
| `AccountManager.Domain.Tests` | Unitário | Regras de crédito/débito e saldo |
| `AccountManager.Application.Tests` | Unitário | Services, queries e command handlers |
| `AccountManager.Integration.Tests` | Integração | Auth, fluxo completo, idempotência, overdraft |

```bash
dotnet test tests/AccountManager.Domain.Tests
dotnet test tests/AccountManager.Application.Tests
dotnet test tests/AccountManager.Integration.Tests
```

> Os testes de integração sobem Postgres e Redis via **Testcontainers** (Docker necessário).

### CI (GitHub Actions)

Workflow em [`.github/workflows/ci.yml`](.github/workflows/ci.yml) — sem deploy (somente CI):

- **Backend:** restore → build Release → testes Domain, Application e Integration  
- **Frontend:** `npm ci` → `npm run build`

Dispara em `push`/`pull_request` nas branches `main`/`master` e via *workflow_dispatch*.

---

## 7. Decisões técnicas

1. **Redis não é fonte de saldo** — apenas lock, idempotência e gate da réplica. Fonte de verdade = Postgres Write.
2. **CQRS explícito** — handlers próprios, sem MediatR, para facilitar explicação na entrevista.
3. **EF na escrita / Dapper na leitura** — transação e consistência no write; consultas leves no read.
4. **Gateway BFF** — contrato estável para o front + RoundRobin observável (`X-Api-Instance`).
5. **Saldo ≥ 0 em três camadas** — domínio, SQL condicional e constraint `CHECK`.
6. **Audit append-only** — registra sucesso e rejeição.
7. **JWT** — rotas de conta autenticadas; ator da auditoria vem do token.

---

## 8. Evoluções futuras

Itens que **não** bloqueiam o escopo do desafio:

- Traces com OpenTelemetry (além de Prometheus)
- Auto-failover Postgres (Patroni)
- Redis Sentinel
- Paginação cursor no histórico
- Descrição livre nas movimentações
- Topologia mínima (1 API + 1 Postgres), se desejado para demo mais simples

---

## Licença / uso

Projeto desenvolvido como entrega do desafio técnico act digital.

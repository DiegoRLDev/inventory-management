# Inventory Management

Sistema mínimo de emissão de notas fiscais com dois microsserviços: **Estoque** e **Faturamento**.

O fechamento da nota depende de uma baixa de estoque síncrona. RabbitMQ entra só depois do sucesso, para auditoria em log.

## Arquitetura

```
Angular
   |
   +--> Inventory.Api --> PostgreSQL
   |
   +--> Billing.Api --> PostgreSQL
             |
             +--> REST --> Inventory.Api
             |
             +--> RabbitMQ --> Audit.Worker
```

Cada serviço tem o próprio banco. Billing não acessa o banco do Inventory.

## Tecnologias

- .NET 8, ASP.NET Core Web API, EF Core, PostgreSQL
- RabbitMQ.Client
- Angular 19.2.25, Angular Material, Reactive Forms

## Estrutura

```
InventoryManagement.sln
src/Inventory.Api
src/Billing.Api
src/Audit.Worker
frontend/inventory-management
tests/InventoryManagement.Tests
docker-compose.yml
docs/TECHNICAL-DETAILS.md
```

## Pré-requisitos

- .NET 8 SDK
- Node.js 20+ (para o Angular)
- Docker Desktop
- `dotnet-ef` (`dotnet tool install --global dotnet-ef`)

## Portas

| Serviço | Porta |
|---|---|
| Inventory.Api | 5081 |
| Billing.Api | 5082 |
| Angular | 4200 |
| PostgreSQL Estoque | 5433 |
| PostgreSQL Faturamento | 5434 |
| RabbitMQ AMQP | 5672 |
| RabbitMQ Management | 15672 (guest/guest) |

## Como executar

### 1. Infraestrutura

```powershell
docker compose up -d postgres-inventory postgres-billing rabbitmq
```

### 2. Migrations

As APIs **não** aplicam migrations sozinhas. Rodar sempre que houver migration pendente.

Inventory hoje tem duas: `InitialCreate` e `AddProcessedDebit`.

```powershell
dotnet ef database update --project src/Inventory.Api
dotnet ef database update --project src/Billing.Api
```

Os comandos usam as connection strings de `appsettings.json` (`localhost:5433` e `localhost:5434`).

### 3. Backend (Docker ou local)

**Docker (infra + APIs + worker):**

```powershell
docker compose up -d --build
```

**Ou local:**

```powershell
dotnet run --project src/Inventory.Api --urls http://localhost:5081
dotnet run --project src/Billing.Api --urls http://localhost:5082
dotnet run --project src/Audit.Worker
```

### 4. Angular

Não está no Docker.

```powershell
cd frontend/inventory-management
npm install
npx ng serve
```

Abrir http://localhost:4200

## Fluxo de emissão

1. Angular chama `POST /api/invoices/{id}/issue` no Billing.
2. Billing verifica se a nota está `Open`.
3. Billing chama `POST /api/stock/debit` no Inventory (REST síncrono, timeout 5s).
4. Se a baixa ok: nota vira `Closed` e Billing publica `InvoiceClosedEvent`.
5. Se Inventory recusar ou estiver fora após as tentativas: nota permanece `Open` e o Angular mostra o erro.

## Idempotência e retry

A baixa usa o `InvoiceId` como chave de idempotência (`ProcessedDebits`, UNIQUE).

**Por quê:** o Inventory pode baixar o estoque e a resposta HTTP se perder. Sem essa chave, um retry do Billing baixaria de novo.

**Retry só depois da idempotência:** repetir a chamada ficou seguro. O Billing tenta até 3 vezes, com 200 ms entre elas, só em falha transitória:

- `HttpRequestException`
- timeout
- HTTP 5xx

**Não repetem** (regra de negócio): 400, 404, 409, 422 (estoque insuficiente, produto inexistente, etc.).

## Como demonstrar falha do Inventory

Com uma nota `Open`:

```powershell
docker compose stop inventory-api
```

Emitir a nota. O Billing tenta até 3 vezes (timeout 5s); a tela pode ficar uns segundos no spinner. Isso não é travamento.

Esperado: HTTP 503, nota continua `Open`, estoque inalterado.

```powershell
docker compose start inventory-api
```

## Como demonstrar idempotência

Depois de emitir uma nota (`Closed`), repetir a baixa com o mesmo `invoiceId`:

```powershell
POST http://localhost:5081/api/stock/debit
```

Corpo: o mesmo `invoiceId` e os mesmos itens. Esperado: HTTP 200 e o saldo **não** cai de novo.

## RabbitMQ e Audit.Worker

Depois da nota `Closed`, Billing publica na fila `invoice.closed` (exchange default).

O worker loga: `Invoice {número} closed at {data}`.

Não persiste auditoria. Se o RabbitMQ estiver fora, a nota **já fechada permanece Closed** (sem Outbox).

## Testes

```powershell
dotnet test InventoryManagement.sln
```

Evento no worker e UI Angular são testes manuais.

## Trade-offs

- Sem Outbox: se o RabbitMQ cair depois do `SaveChanges`, a nota fica `Closed` e o evento pode se perder.
- Sem concorrência avançada de estoque: duas baixas simultâneas de invoices **diferentes** no mesmo saldo não foram tratadas.
- Numeração da nota usa sequence PostgreSQL `invoice_numbers` (`nextval`), não `MAX+1`.

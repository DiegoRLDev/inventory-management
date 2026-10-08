# Detalhes técnicos

Notas sobre as decisões técnicas da aplicação.

## Angular

**Versão:** 19.2.25 (standalone components).

**Lifecycle:** `ngOnInit` nas três telas, para carregar dados ao entrar na rota.

| Componente | Hook | Uso |
|---|---|---|
| `ProductsPageComponent` | `ngOnInit` | `ProductService.list()` |
| `InvoiceCreatePageComponent` | `ngOnInit` | carregar produtos para o select |
| `InvoicesPageComponent` | `ngOnInit` | `InvoiceService.list()` |

Não há `ngOnDestroy` nem subscriptions longas: cada chamada HTTP termina no `subscribe` da ação.

**RxJS:** apenas o `Observable` do `HttpClient`. As telas usam `subscribe` para sucesso/erro. Sem `switchMap`, NgRx ou store.

**Reactive Forms:** `FormBuilder.nonNullable.group` no cadastro de produto (`code`, `description`, `balance`) e no item da nota (`productId`, `quantity`), com `Validators.required` / `min`.

**Angular Material:** toolbar, table, form-field, input, select, button, progress-spinner, snack-bar. Tema prebuilt `azure-blue`.

**Outras bibliotecas Angular:** `@angular/cdk` e `@angular/animations` (dependências do Material). Sem NgRx.

## Backend

**Framework:** .NET 8 / ASP.NET Core Web API.

**Persistência:** EF Core 8 + Npgsql + PostgreSQL. Um `DbContext` por serviço. No Inventory, `Products` e `ProcessedDebits`.

**DI:** registro no `Program.cs` (`AddDbContext`, `AddScoped<StockService>`, `AddHttpClient<InventoryClient>`, `AddSingleton<InvoiceClosedPublisher>`).

**Typed HttpClient:** `InventoryClient` no Billing, `IHttpClientFactory`, timeout 5s. Sem Polly: loop de até 3 tentativas, 200 ms entre elas.

Retry só existe porque a baixa é idempotente pelo `InvoiceId`. Repete apenas falha transitória (`HttpRequestException`, timeout, HTTP 5xx). Não repete 400, 404, 409 nem 422.

**Idempotência da baixa:** tabela `ProcessedDebits` (`InvoiceId` UNIQUE). Evita baixa duplicada se a resposta HTTP se perder e o Billing chamar de novo. Se o invoice já foi processado, devolve sucesso sem alterar saldo. Baixa e registro na mesma transação. UNIQUE cobre duas requisições iguais ao mesmo tempo.

**LINQ:** consultas EF (`Where`/`OrderBy`/`Include`/`AnyAsync`/`FirstOrDefaultAsync`) e agrupamento da baixa (`GroupBy` das quantidades por produto).

**Erros:**

- Data Annotations + `[ApiController]` → 400
- código duplicado → 409
- estoque insuficiente → 422
- nota já fechada → 409
- Inventory down → 503 (`HttpRequestException`), depois de até 3 tentativas
- timeout Inventory → 504 (`TaskCanceledException`), depois de até 3 tentativas
- Angular lê `error.error` e mostra no snackbar

Não há middleware global de exceção além disso.

**RabbitMQ.Client 7.2.2:** fila durável `invoice.closed`, exchange default. Publicação só depois do commit `Closed`. Worker consome, loga e dá ack. Sem Outbox, DLQ ou persistência de auditoria.

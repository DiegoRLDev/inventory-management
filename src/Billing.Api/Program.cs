using System.Text.Json.Serialization;
using Billing.Api.Clients;
using Billing.Api.Data;
using Billing.Api.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Billing")));

var inventoryBaseUrl = builder.Configuration["Inventory:BaseUrl"]
    ?? throw new InvalidOperationException("Inventory:BaseUrl is not configured.");
var inventoryTimeoutSeconds = builder.Configuration.GetValue("Inventory:TimeoutSeconds", 5);

builder.Services.AddHttpClient<InventoryClient>(client =>
{
    client.BaseAddress = new Uri(inventoryBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(inventoryTimeoutSeconds);
});

builder.Services.AddSingleton<InvoiceClosedPublisher>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.MapGet("/health", () => Results.Ok(new { service = "Billing.Api" }));
app.MapControllers();

app.Run();

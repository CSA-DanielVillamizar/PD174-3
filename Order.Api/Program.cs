using Order.Api;
using System.Net.Http.Json;
using MassTransit; // <-- NUEVO IMPORT 
using Itm.Shared.Events; // <-- NUEVO IMPORT
using Microsoft.AspNetCore.Identity; // <-- NUEVO IMPORT para Identity (si decides usarlo en el futuro)
using Microsoft.AspNetCore.Diagnostics.HealthChecks; // <-- NUEVO IMPORT para Health Checks
using Microsoft.Extensions.Diagnostics.HealthChecks; // <-- NUEVO IMPORT para Health Checks
using HealthChecks.UI.Client; // <-- NUEVO IMPORT para respuesta JSON estándar de HealthChecks UI
using Itm.Order.Api.Handlers;
using Microsoft.EntityFrameworkCore;
using Order.Api.Data;
using Order.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Configurar el contexto de base de datos SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Usamos el servicio de Kubernetes sql-service creado
    options.UseSqlServer("Server=sql-service;Database=OrdersDb;User Id=sa;Password=ItmMaui_Ticket$2026;TrustServerCertificate=True;");
});

// Servicios básicos y Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

// CONFIGURACIÓN DEL PRODUCTOR (MassTransit)
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        // En un trabajo real, esta URL debe venir de configuración segura (KeyVault / env vars)
        cfg.Host("amqp://rabbitmq-service:5672");
    });
});

// Cliente HTTP hacia Inventory.Api
builder.Services.AddHttpClient("InventoryClient", client =>
{
    // CORRECCIÓN: Usamos el DNS interno de Kubernetes para contactar a Inventory Service
    // El formato en Kubernetes es: http://<nombre-del-servicio>.<namespace>.svc.cluster.local
    // o simplemente http://<nombre-del-servicio> (en caso de usar el puerto 80 del ClusterIP)
    client.BaseAddress = new Uri("http://inventory-api-service:80");
})
    // Propagamos el X-Correlation-ID hacia Inventory.Api
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

// 1.  Registrar el servicio de salud con un check real contra CloudAMQP
builder.Services.AddHealthChecks()
    .AddCheck<CloudAmqpHealthCheck>("CloudAMQP-Broker");

var app = builder.Build(); // Linea divisoria entre configuración y pipeline

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Aplicar migraciones automáticamente si la DB no existe
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// NUEVO: Endpoint GET para la prueba Nivel 5 usando la base de datos real
app.MapGet("/orders/api/orders", async (AppDbContext db, HttpContext httpContext, ILogger<Program> logger) =>
{
    var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "SIN-ID";
    logger.LogInformation("Consulta de órdenes recibida mediante GET desde DB local en K8s. CorrelationId: {CorrelationId}", correlationId);

    var orders = await db.Orders.ToListAsync();
    return Results.Ok(orders);
});

//  Agregamos IPublishEndpoint y acceso a HttpContext/ILogger a los parámetros
app.MapPost(
    "/orders/api/orders",
    async (CreateOrderDto order, AppDbContext db, IHttpClientFactory factory, IPublishEndpoint publisher, HttpContext httpContext, ILogger<Program> logger) =>
    {
        // Extraemos el Correlation ID que viene desde el Gateway o lo marcamos como SIN-ID
        var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "SIN-ID";

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        logger.LogInformation("Iniciando procesamiento de la orden para el producto {ProductId}", order.ProductId);

        var invClient = factory.CreateClient("InventoryClient");

        // Paso 1: Intentar reservar el stock
        var reduceResponse = await invClient.PostAsJsonAsync("/api/inventory/reduce", order);

        if (!reduceResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("No se pudo reservar el stock para el producto {ProductId}. StatusCode: {StatusCode}", order.ProductId, reduceResponse.StatusCode);
            return Results.BadRequest("No se pudo reservar el stock. Transacción abortada.");
        }

        try
        {
            // Paso 2: Procesar el pago (simulado con un random para este ejemplo)
            bool paymentSuccess = true; // Forzado a true para Demo de Load Testing y Black Friday

            if (!paymentSuccess)
            {
                throw new InvalidOperationException("Fondos Insuficientes en la Tarjeta");
            }

            // Supongamos que la venta fue exitosa y ya cobraron.
            var newOrderId = Guid.NewGuid(); // Simulamos el ID generado
            decimal finalTotal = 150000m;    // Simulamos el total de la venta

            // GUARDAR EN BASE DE DATOS SQL SERVER
            var newOrderEntity = new OrderEntity
            {
                Id = newOrderId,
                ProductId = order.ProductId,
                Quantity = order.Quantity,
                Status = "Completada/PendienteNotificación",
                FinalTotal = finalTotal
            };
            db.Orders.Add(newOrderEntity);
            await db.SaveChangesAsync();
            logger.LogInformation("Orden {OrderId} almacenada exitosamente en la base de datos SQL.", newOrderId);

            // ---------------------------------------------------------
            // EMISIÓN DEL EVENTO (Patrón Fire and Forget)
            // ---------------------------------------------------------
            // Empacamos la caja
            var orderEvent = new OrderCreatedEvent(newOrderId, order.ProductId, "usuario@correo.itm.edu", finalTotal);

            // La tiramos al buzón de RabbitMQ en la nube
            await publisher.Publish(orderEvent);

            logger.LogInformation("Evento publicado en RabbitMQ. Orden {OrderId} completada.", newOrderId);

            return Results.Ok(new { Status = "Orden procesada rápido", OrderId = newOrderId, CorrelationId = correlationId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el pago para el producto {ProductId}. Iniciando compensación de stock...", order.ProductId);

            // INCIO DE LA COMPENSACIÓN (SAGA ROLLBACK)
            var compensateResponse = await invClient.PostAsJsonAsync("/api/inventory/release", order);
            if (compensateResponse.IsSuccessStatusCode)
            {
                return Results.Problem("El pago falló. El sttock due devuelto correctamente. Intente de nuevo.");
            }

            logger.LogCritical("Falló la compensación. Datos inconsistentes para el producto {ProductId}.", order.ProductId);
            return Results.Problem("Error crítico del sistema. Contacte soporte.");
        }
    });

// 2. Exponer el endpoint de salud en formato JSON estándar para HealthChecks UI
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();

// Health check que verifica conectividad TCP básica contra el broker de CloudAMQP
internal sealed class CloudAmqpHealthCheck : IHealthCheck
{
    private const string AmqpUrl = "amqp://rabbitmq-service:5672";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = new Uri(AmqpUrl);
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : 5671, cancellationToken);
            return HealthCheckResult.Healthy("CloudAMQP reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("CloudAMQP unreachable", ex);
        }
    }
}

using Itm.Inventory.Api.Dtos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using HealthChecks.UI.Client;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Servicios básicos (Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Bloque de seguridad JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],

            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
    });

builder.Services.AddAuthorization();

// Agregamos el Accessor para poder leer las cabeceras
builder.Services.AddHttpContextAccessor();

//1. Registar el servicio de salud
// Estamos enseñando a nuestra aplicacion a tomarse el pulso a si misma, para que herramientas externas puedan saber si esta funcionando correctamente o no.
builder.Services.AddHealthChecks();

var app = builder.Build();

// 3. Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Primero autenticación, luego autorización
app.UseAuthentication();
app.UseAuthorization();

// 4. "Base de datos" en memoria
var inventoryDb = new List<InventoryItemDto>
{
    new(1, 150000, "LAPTOP-DELL"),
    new(2, 0, "MOUSE-LOGI"),
    new(3, 100, "TECLADO-RGB")
};

// 5. Endpoints
app.MapGet("/api/inventory/{id}", (int id, HttpContext httpContext, ILogger<Program> logger) =>
{
    // 1. Extraemos el Correlation ID
    var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "SIN-ID";

    // 2. Sellamos el log con el pasaporte
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        logger.LogInformation("Consultando inventario en BD para el producto {ProductId}", id);

        var item = inventoryDb.FirstOrDefault(p => p.ProductId == id);
        return item is not null ? Results.Ok(item) : Results.NotFound();
    }
})
.WithName("GetInventory")
.WithOpenApi();

app.MapPost("/api/inventory/reduce", (ReduceStockRequestDto request, HttpContext httpContext, ILogger<Program> logger) =>
{
    var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "SIN-ID";

    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        logger.LogInformation("Reduciendo inventario para el producto {ProductId}. Cantidad: {Quantity}", request.ProductId, request.Quantity);

        // Simulamos la lógica de reducción de stock
        var itemIndex = inventoryDb.FindIndex(p => p.ProductId == request.ProductId);
        if (itemIndex == -1)
        {
            logger.LogWarning("Producto {ProductId} no encontrado en el inventario.", request.ProductId);
            return Results.NotFound(new { Message = "Producto no encontrado." });
        }

        var item = inventoryDb[itemIndex];
        if (item.Stock < request.Quantity)
        {
            logger.LogWarning("Stock insuficiente para el producto {ProductId}. Requerido: {Quantity}, Disponible: {Stock}", request.ProductId, request.Quantity, item.Stock);
            return Results.BadRequest(new { Message = "Estás intentando pedir más de lo que tenemos." });
        }

        // En un entorno de producción, esto debería ser atómico con la BD real
        var updatedItem = item with { Stock = item.Stock - request.Quantity };
        inventoryDb[itemIndex] = updatedItem;

        logger.LogInformation("Stock reducido exitosamente. Nuevo stock para {ProductId}: {Stock}", request.ProductId, updatedItem.Stock);
        return Results.Ok(updatedItem);
    }
})
.WithName("ReduceInventory")
.WithOpenApi();

app.MapPost("/api/inventory/release", (ReleaseStockRequestDto request, HttpContext httpContext, ILogger<Program> logger) =>
{
    var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "SIN-ID";

    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        logger.LogInformation("Restaurando inventario (Rollback) para el producto {ProductId}. Cantidad: {Quantity}", request.ProductId, request.Quantity);

        var itemIndex = inventoryDb.FindIndex(p => p.ProductId == request.ProductId);
        if (itemIndex == -1)
        {
            logger.LogWarning("Producto {ProductId} no encontrado. Imposible realizar Rollback.", request.ProductId);
            return Results.NotFound(new { Message = "Producto no encontrado para restablecer." });
        }

        var item = inventoryDb[itemIndex];
        var updatedItem = item with { Stock = item.Stock + request.Quantity };
        inventoryDb[itemIndex] = updatedItem;

        logger.LogInformation("Stock compensado exitosamente (SAGA). Nuevo stock para {ProductId}: {Stock}", request.ProductId, updatedItem.Stock);
        return Results.Ok(updatedItem);
    }
})
.WithName("ReleaseInventory")
.WithOpenApi();

// 2. Exponer el endpoint de salud en formato JSON compatible con HealthChecks UI
// Abrimos la "Puerta" en el servidor para que el Gateway (o nosotros) pueda preguntar por la salud.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});


app.Run();
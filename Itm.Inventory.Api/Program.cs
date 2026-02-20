using Itm.Inventory.Api.Dtos; // Asegúrate de que este namespace coincida con donde creaste tu DTO

var builder = WebApplication.CreateBuilder(args);

// 1. AGREGAR SERVICIOS (El Contenedor de Inyección de Dependencias)
// Agregamos Swagger para poder probar visualmente (muy útil en desarrollo)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 2. CONFIGURAR EL PIPELINE (Middleware)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ---------------------------------------------------------
// 3. SIMULACIÓN DE BASE DE DATOS (En memoria para la clase)
// ---------------------------------------------------------
// Nota de Arquitecto: En la vida real, esto se inyectaría como un IRepository
var inventoryDb = new List<InventoryItemDto>
{
    new(1, 50, "LAPTOP-DELL"),
    new(2, 0, "MOUSE-LOGI"), // Sin stock para probar fallos
    new(3, 100, "TECLADO-RGB")
};

// ---------------------------------------------------------
// 4. ENDPOINTS (Minimal API)
// ---------------------------------------------------------

// Endpoint para consultar inventario
app.MapGet("/api/inventory/{id}", (int id) =>
{
    // Buscamos en la lista simulada
    var item = inventoryDb.FirstOrDefault(p => p.ProductId == id);

    // Retornamos 200 OK si existe, o 404 NotFound si no
    return item is not null ? Results.Ok(item) : Results.NotFound();
})
.WithName("GetInventory") // Nombre para documentación Swagger
.WithOpenApi();

app.Run();
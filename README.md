# Itm.Distributed.System

Sistema distribuido de ejemplo para el curso de Arquitectura de Software (Clases 1 y 2).

Este proyecto muestra cómo pasar de un monolito a una arquitectura de microservicios sencilla usando .NET 8 y Minimal APIs:

- `Itm.Inventory.Api` – Servicio de Inventario (dueño del stock)
- `Itm.Price.Api` – Servicio de Precios (dueño del dinero)
- `Itm.Product.Api` – Orquestador / BFF que compone información de los otros dos

## 1. Requisitos

- Visual Studio 2022 o superior (carga de trabajo "Desarrollo ASP.NET y Web")
- SDK .NET 8.0 instalado

Verificar SDK:

```bash
dotnet --version
```

## 2. Proyectos

### 2.1. Itm.Inventory.Api

Microservicio que expone el stock disponible de cada producto.

- DTO principal: `Itm.Inventory.Api/Dtos/InventoryDto.cs`
- Endpoint:

```http
GET /api/inventory/{id}
```

Ejemplo de respuesta:

```json
{
  "productId": 1,
  "stock": 50,
  "sku": "LAPTOP-DELL"
}
```

### 2.2. Itm.Price.Api

Microservicio que expone el precio y la moneda de cada producto.

- DTO principal: `Itm.Price.Api/Dtos/PriceDto.cs`
- Endpoint:

```http
GET /api/prices/{id}
```

Ejemplo de respuesta:

```json
{
  "productId": 1,
  "amount": 1500.0,
  "currency": "USD"
}
```

### 2.3. Itm.Product.Api

Orquestador (Backend for Frontend, BFF). No tiene base de datos propia; consume `Inventory` y `Price` para devolver un JSON agregado.

Clientes HTTP configurados en `Itm.Product.Api/Program.cs`:

```csharp
builder.Services.AddHttpClient("InventoryClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5293");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient("PriceClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5012");
});
```

Endpoints principales:

1. Solo inventario

```http
GET /api/products/{id}/check-stock
```

2. Resumen completo (paralelo: inventario + precios)

```http
GET /api/products/{id}/summary
```

Ejemplo de respuesta del resumen:

```json
{
  "id": 1,
  "product": "Laptop Gamer Pro",
  "stockDetails": {
    "productId": 1,
    "stock": 50,
    "sku": "LAPTOP-DELL"
  },
  "financialDetails": {
    "productId": 1,
    "amount": 1500.0,
    "currency": "USD"
  },
  "calculatedAt": "2026-02-20T10:00:00Z"
}
```

## 3. Cómo ejecutar el ecosistema

1. Configurar proyectos de inicio múltiples en la solución:
   - `Itm.Inventory.Api` – Iniciar
   - `Itm.Price.Api` – Iniciar
   - `Itm.Product.Api` – Iniciar

2. Verificar puertos actuales (por defecto en este repo):
   - Inventory: `http://localhost:5293`
   - Price: `http://localhost:5012`
   - Product: `http://localhost:5298`

3. Ejecutar la solución desde Visual Studio o con CLI:

```bash
dotnet run --project Itm.Inventory.Api/Itm.Inventory.Api.csproj
 dotnet run --project Itm.Price.Api/Itm.Price.Api.csproj
 dotnet run --project Itm.Product.Api/Itm.Product.Api.csproj
```

4. Probar desde navegador o curl:

```bash
curl http://localhost:5298/api/products/1/summary
```

## 4. Conceptos de arquitectura cubiertos

- Monolito vs. Microservicios
- Bajo acoplamiento usando DTOs (`record` inmutables)
- Uso de `HttpClientFactory` en .NET
- Patrón API Composition / Backend for Frontend
- Paralelismo con `Task.WhenAll`
- Manejo básico de errores en sistemas distribuidos

Este repo cubre las dos primeras clases del curso y sirve como base para futuras sesiones sobre resiliencia, seguridad (JWT/API Keys) y observabilidad.
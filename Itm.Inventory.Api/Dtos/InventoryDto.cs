namespace Itm.Inventory.Api.Dtos
{
    // Usamos 'record' para inmutabilidad y sintaxis concisa
    public record InventoryItemDto(int ProductId, int Stock, string Sku);
}
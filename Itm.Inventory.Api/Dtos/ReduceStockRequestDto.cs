namespace Itm.Inventory.Api.Dtos;

public record ReduceStockRequestDto(int ProductId, int Quantity);
using System;

namespace Order.Api.Models;

public class OrderEntity
{
    public Guid Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal FinalTotal { get; set; }
}

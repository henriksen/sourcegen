using Annotations;

namespace App;

[StrongId]
public readonly partial struct OrderId;

public sealed class Order
{
    public OrderId Id { get; set; }
    public string Status { get; set; } = "";
    public int Quantity { get; set; }
}
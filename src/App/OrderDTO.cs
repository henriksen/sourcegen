using Annotations;

namespace App;

[MapFrom(typeof(Order))]
public sealed partial class OrderDto
{
    public OrderId Id { get; set; }      // same type → direct assign
    public string Status { get; set; } = "";
    public int Quantity { get; set; }    
    //public string? Note { get; set; }    // no source → generator will DIAGNOSE
}
using System.ComponentModel.DataAnnotations;
using Annotations;

namespace App;

[ConfigSection("Payments")]
public sealed partial class PaymentsOptions
{
    public string ApiKey { get; set; } = default!;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; }
}
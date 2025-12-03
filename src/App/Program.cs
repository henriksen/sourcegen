using Annotations;
using App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine(Hello.Message); // Stage 0: generated


// Build configuration (loads appsettings.json)
var cfg = new ConfigurationBuilder()
	.AddJsonFile("appsettings.json", optional: true)
	.AddEnvironmentVariables()
	.Build();

var services = new ServiceCollection()
	.AddPaymentsOptions(cfg); // <-- generated: Add{TypeName}(IServiceCollection, IConfiguration)

using var sp = services.BuildServiceProvider();

// Prove it's bound + validated
var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentsOptions>>().Value;
Console.WriteLine($"Payments.ApiKey: {opts.ApiKey}, Timeout: {opts.TimeoutSeconds}");

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Security.KeyVault.Secrets;
using System;
using Azure.Identity;
using Microsoft.Azure.Devices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((ctx, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices(services =>
    {
        string akvUri = Environment.GetEnvironmentVariable("KeyVaultEndpoint");
        if (string.IsNullOrEmpty(akvUri)) throw new ArgumentNullException("Failed to get KeyVaultEndpoint setting from the environment. Please check configuration.");

        services.AddSingleton<SecretClient>(
            o => new SecretClient(
                vaultUri: new Uri(akvUri),
                credential: new DefaultAzureCredential()));

        string iotHub = Environment.GetEnvironmentVariable("HubConnectionString");
        if (string.IsNullOrEmpty(iotHub)) throw new ArgumentNullException("Failed to get HubConnectionString setting from the environment. Please check configuration.");
        services.AddSingleton<RegistryManager>(o => RegistryManager.CreateFromConnectionString(iotHub));
    })
    .Build();

host.Run();
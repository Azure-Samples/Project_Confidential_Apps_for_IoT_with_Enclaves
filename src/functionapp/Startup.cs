using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Microsoft.Azure.Devices;

[assembly: FunctionsStartup(typeof(functionapp.Startup))]

namespace functionapp;
public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        string? akvUri = Environment.GetEnvironmentVariable("KeyVaultEndpoint");
        if (string.IsNullOrEmpty(akvUri)) throw new ArgumentNullException("Failed to get KeyVaultEndpoint setting from the environment. Please check configuration.");

        string? managedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        builder.Services.AddSingleton<SecretClient>(
            o => new SecretClient(
                vaultUri: new Uri(akvUri),
                credential: managedIdentityClientId != null ?
                    new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = managedIdentityClientId }) :
                    new DefaultAzureCredential()));

        string? iotHub = Environment.GetEnvironmentVariable("HubConnectionString");
        if (string.IsNullOrEmpty(iotHub)) throw new ArgumentNullException("Failed to get HubConnectionString setting from the environment. Please check configuration.");
        builder.Services.AddSingleton<RegistryManager>(o => RegistryManager.CreateFromConnectionString(iotHub));
    }
}
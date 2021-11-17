using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Security.KeyVault.Secrets;
using System;
using Azure.Identity;
using Microsoft.Azure.Devices;
using System.Collections.Generic;
using System.Linq;

namespace functionapp
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureAppConfiguration((ctx, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true);
                    config.AddJsonFile("local.settings.json", optional: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices(services =>
                {
                    string akvUri = Environment.GetEnvironmentVariable("KeyVaultEndpoint");
                    if(string.IsNullOrEmpty(akvUri)) throw new ArgumentNullException("Failed to get KeyVaultEndpoint setting from the environment. Please check configuration.");

                    services.AddSingleton<SecretClient>(
                        o => new SecretClient(
                            vaultUri: new Uri(akvUri),
                            credential:new DefaultAzureCredential()));

                    string iotHub = Environment.GetEnvironmentVariable("HubConnectionString");
                    if(string.IsNullOrEmpty(iotHub)) throw new ArgumentNullException("Failed to get HubConnectionString setting from the environment. Please check configuration.");
                    services.AddSingleton<RegistryManager>(o => RegistryManager.CreateFromConnectionString(iotHub));

                    string secrets = Environment.GetEnvironmentVariable("KeyVaultSecretNames");
                    if(string.IsNullOrEmpty(iotHub)) throw new ArgumentNullException("Failed to get KeyVaultSecretNames setting from the environment. Please check configuration.");
                    services.AddTransient<List<string>>(o => secrets.Split(',').ToList());
                })
                .Build();

            host.Run();
        }
    }
}
using System.Threading.Tasks;
using System.Security.Cryptography;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using functionapp.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using System.Linq;

namespace functionapp;

public class Functions
{
    private readonly ILogger logger;
    private readonly SecretClient secretClient;
    private readonly RegistryManager registryManager;

    public Functions(ILoggerFactory loggerFactory, SecretClient secretClient, RegistryManager registryManager)
    {
        this.logger = loggerFactory.CreateLogger<Functions>();
        this.secretClient = secretClient;
        this.registryManager = registryManager;
    }

    [Function("ProvisionKey")]
    public async Task<HttpResponseData> ProvisionKey([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData request, string key_name)
    {
        this.logger.LogInformation("ProvisionKey function processed a request.");

        Key key = await request.GetKey(key_name);

        if (!key.IsValid(out List<ValidationResult> errors)) return await request.BadRequestObjectResult(errors);

        using (Aes myAes = Aes.Create())
        {
            myAes.KeySize = 256;
            myAes.GenerateKey();
            byte[] rawKey = myAes.Key;
            await this.secretClient.SetSecretAsync(key.KeyName, Convert.ToBase64String(rawKey));
        }

        return await request.OkObjectResult<Response>(new Response { Message = $"Successfully provisioned the key {key.KeyName} in {secretClient.VaultUri}." });
    }

    [Function("WrapKey")]
    public async Task<HttpResponseData> WrapKey([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData request, string key_name, string client_public_key = null)
    {
        this.logger.LogInformation("WrapKey function processed a request.");

        Key key = await request.GetKey(key_name, client_public_key);

        if (!key.IsValid(out List<ValidationResult> errors, true)) return await request.BadRequestObjectResult(errors);

        KeyVaultSecret secret = await this.secretClient.GetSecretAsync(key.KeyName);
        using (var rsa = key.GetRSACryptoServiceProvider())
        {
            byte[] wrappedKey = rsa.Encrypt(Convert.FromBase64String(secret.Value), false);

            return await request.OkObjectResult<Response>(
                new Response { WrappedKey = Convert.ToBase64String(wrappedKey) });
        }

    }

    [Function("DistrubuteKey")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] MyInfo info)
    {
        this.logger.LogInformation($"DistrubuteKey Timer trigger function executed at: {DateTime.Now}");

        List<SecretProperties> secrets = this.secretClient.GetPropertiesOfSecrets().ToList();

        Dictionary<string, string> map = secrets.Select(
            s => this.secretClient.GetSecretAsync(s.Name).Result.Value).ToDictionary(k => k.Name, v => v.Value);

        this.logger.LogInformation($"Obtained {map.Count} key(s) for distribution.");

        IQuery query = this.registryManager.CreateQuery("SELECT * FROM devices WHERE is_defined(properties.reported.device_public_key)", 100);

        while (query.HasMoreResults)
        {
            IEnumerable<Twin> page = await query.GetNextAsTwinAsync();
            foreach (Twin twin in page)
            {
                this.logger.LogInformation($"Distributing key(s) to twin of device '{twin.DeviceId}'.");

                Key key = new Key
                {
                    KeyName = twin.DeviceId,
                    ClientPublicKeyBase64Encoded = twin.Properties.Reported["device_public_key"].ToString()
                };

                using (var rsa = key.GetRSACryptoServiceProvider())
                {
                    twin.Properties.Desired["confidential_package_keys"] = map.ToDictionary(
                        k => k.Key,
                        v => Convert.ToBase64String(rsa.Encrypt(Convert.FromBase64String(v.Value), false)));
                }
                _ = await registryManager.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
            }
        }

        this.logger.LogInformation("Successful execution of the DistributeKey timer hook.");
    }
}
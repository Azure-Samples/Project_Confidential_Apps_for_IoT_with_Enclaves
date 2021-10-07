#r "Newtonsoft.Json"

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

// Imports needed to talk to device twins
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;

// Imports needed to authenticate to Azure key vault
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

// BouncyCastle for crypto utilities
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

// Given a dictionary of secret names mapping to their string values, this function returns the equivalent
// map of the same key names mapping to their encrypted values, using the given publicKey, which is assumed
// to be the base64 of a SubjectPublicKeyInfo corresponding to an RSA public key. The encrypted values are
// also base64 encoded on completion
static Dictionary<string, string> WrapAll(Dictionary<string, string> source, string publicKey, ILogger log)
{
    Dictionary<string, string> result = new Dictionary<string, string>();

    // Get an RSA encryption engine from the public key.
    // First, assume it's base64.
    byte[] publicKeyBytes = Convert.FromBase64String(publicKey);

    // This should work provided that the public key is a DER of SubjectPublicKeyInfo.
    AsymmetricKeyParameter asymmetricKeyParameter = PublicKeyFactory.CreateKey(publicKeyBytes);
    RsaKeyParameters rsaKeyParameters = (RsaKeyParameters) asymmetricKeyParameter;
    RSAParameters rsaParameters = new RSAParameters();
    rsaParameters.Modulus = rsaKeyParameters.Modulus.ToByteArrayUnsigned();
    rsaParameters.Exponent = rsaKeyParameters.Exponent.ToByteArrayUnsigned();
    RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
    rsa.ImportParameters(rsaParameters);

    // Encrypt each secret with the public key.
    foreach (KeyValuePair<string, string> kvp in source)
    {
        byte[] raw = Convert.FromBase64String(kvp.Value);
        byte[] encrypted = rsa.Encrypt(raw, false);
        result.Add(kvp.Key, Convert.ToBase64String(encrypted));
    }

    return result;
}

public static void Run(TimerInfo myTimer, ILogger log)
{
    log.LogInformation($"Key distribution function triggered at time index: {DateTime.Now}");

    // Get the connection string to the IoT hub. This should be stored in the environment.
    var connectionString = Environment.GetEnvironmentVariable("HubConnectionString");

    if (string.IsNullOrEmpty(connectionString))
    {
        log.LogError("Failed to get HubConnectionString setting from the environment. Please check configuration.");
        return;
    }

    // Get the key vault endpoint. This should be stored in the environment also.
    var keyVaultEndpoint = Environment.GetEnvironmentVariable("KeyVaultEndpoint");

    if (string.IsNullOrEmpty(keyVaultEndpoint))
    {
        log.LogError("Failed to get KeyVaultEndpoint setting from the environment. Please check configuration.");
        return;
    }

    // Last bit of config: the names of the secrets in the key vault that we want to distribute/wrap. This
    // should be a comma-separated list of names so that we can distribute more than one value to the twins.
    var secretNames = Environment.GetEnvironmentVariable("KeyVaultSecretNames");

    if (string.IsNullOrEmpty(secretNames))
    {
        log.LogError("Failed to get KeyVaultSecretNames setting from the environment. Please check configuration.");
        return;
    }

    var secretNamesArray = secretNames.Split(',');

    // Get ready to access the Azure Key Vault.
    var serviceTokenProvider = new AzureServiceTokenProvider();
    var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));

    // Get the secrets out of the vault and store in function-local memory for distribution to the devices.
    Dictionary<string, string> secMap = new Dictionary<string, string>();

    foreach (string s in secretNamesArray)
    {
        try
        {
            var secret = keyVaultClient.GetSecretAsync(keyVaultEndpoint, s).Result;
            secMap.Add(s, secret.Value);
        }
        catch (Exception e)
        {
            log.LogError($"DistributeKey: Failed to obtain secret {s} from key vault {keyVaultEndpoint} due to {e}.");
            // Skip this secret and continue.
        }
    }

    log.LogInformation($"Obtained {secMap.Count} key(s) for distribution.");

    // Get the RegistryManager from the connection string. This lets us talk to the device twins.
    using (var registryManager = RegistryManager.CreateFromConnectionString(connectionString))
    {
        // Iterate over all devices that have published a 'device_public_key' property.
        var query = registryManager.CreateQuery("SELECT * FROM devices WHERE is_defined(properties.reported.device_public_key)", 100);
        while (query.HasMoreResults)
        {
            var page = query.GetNextAsTwinAsync().Result;
            foreach (var twin in page)
            {
                log.LogInformation($"Distributing key(s) to twin of device '{twin.DeviceId}'.");

                var publicKey = twin.Properties.Reported["device_public_key"];
                var wrappedSecrets = WrapAll(secMap, publicKey.ToString(), log);

                // Patch the twin with the table of wrapped keys as a desired property.
                twin.Properties.Desired["confidential_package_keys"] = wrappedSecrets;

                var updatedTwin = registryManager.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag).Result;
            }
        }
    }

    log.LogInformation("Successful execution of the DistributeKey timer hook.");
}

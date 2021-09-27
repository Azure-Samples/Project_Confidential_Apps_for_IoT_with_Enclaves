#r "Newtonsoft.Json"

using System;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

    // Get the key vault endpoint. This should be stored in the environment also.
    var keyVaultEndpoint = Environment.GetEnvironmentVariable("KeyVaultEndpoint");

    if (string.IsNullOrEmpty(keyVaultEndpoint))
    {
        log.LogError("Failed to get KeyVaultEndpoint setting from the environment. Please check configuration.");
        return new BadRequestObjectResult("KeyVaultEndpoint not specified in function app config.");
    }

    string name = req.Query["key_name"];

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    dynamic data = JsonConvert.DeserializeObject(requestBody);
    name = name ?? data?.key_name;
    string base64Key;

    if (string.IsNullOrEmpty(name))
    {
        log.LogError("Bad request: no key_name passed in the request.");
        return new BadRequestObjectResult("No key name specified in request.");
    }

    // Locally provision a random AES key
    using (AesCryptoServiceProvider myAes = new AesCryptoServiceProvider())
    {
        myAes.KeySize = 256;
        myAes.GenerateKey();
        byte[] rawKey = myAes.Key;
        base64Key = Convert.ToBase64String(rawKey);
    }

    // Get ready to talk to the key vault.
    var serviceTokenProvider = new AzureServiceTokenProvider();
    var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));

    // Set (add or update) the secret in the client
    var result = keyVaultClient.SetSecretAsync(keyVaultEndpoint, name, base64Key).Result;

    string responseMessage =
        $"Successfully provisioned the key {name} in {keyVaultEndpoint}.";

    return new OkObjectResult(responseMessage);
}

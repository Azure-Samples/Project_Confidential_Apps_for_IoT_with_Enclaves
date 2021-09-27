#r "Newtonsoft.Json"

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

// Imports needed to authenticate to Azure key vault
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

// Imports needed for key wrapping
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("WrapKey function trigger.");

    // Get the key vault endpoint. This should be stored in the environment also.
    var keyVaultEndpoint = Environment.GetEnvironmentVariable("KeyVaultEndpoint");

    if (string.IsNullOrEmpty(keyVaultEndpoint))
    {
        log.LogError("Failed to get KeyVaultEndpoint setting from the environment. Please check configuration.");
        return new BadRequestObjectResult("No key vault endpoint configured for this function app.");
    }

    string keyName = req.Query["key_name"];
    string clientPublicKey = req.Query["client_public_key"];

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    dynamic data = JsonConvert.DeserializeObject(requestBody);
    keyName = keyName ?? data?.key_name;
    clientPublicKey = clientPublicKey ?? data?.client_public_key;

    if (string.IsNullOrEmpty(keyName))
    {
        log.LogError("Key name is null or empty.");
        return new BadRequestObjectResult("Key name invalid.");
    }

    if (string.IsNullOrEmpty(clientPublicKey))
    {
        log.LogError("Client public key is null or empty.");
        return new BadRequestObjectResult("Client public key is null or empty.");
    }

    // Make an RSA crypto engine from the public key
    byte[] publicKeyBytes;

    // Transform the input from base64, and fail the function if this is invalid.
    try
    {
        publicKeyBytes = Convert.FromBase64String(clientPublicKey.ToString());
    }
    catch (FormatException e)
    {
        log.LogError("Client public key is invalid base64.");
        return new BadRequestObjectResult("Client public key is invalid base64.");
    }

    AsymmetricKeyParameter asymmetricKeyParameter;
    
    // Transofmr the decoded base64 into a public key, and fail the function if it is invalid.
    try
    {
        asymmetricKeyParameter = PublicKeyFactory.CreateKey(publicKeyBytes);
    }
    catch (Exception e)
    {
        log.LogError("Client public key is invalid SubjectPublicKeyInfo ASN.1 structure.");
        return new BadRequestObjectResult("Client public key is not a SubjectPublicKeyInfo.");
    }

    // Turn the public key into an RSA engine - any exceptions here are InternalServerError
    RsaKeyParameters rsaKeyParameters = (RsaKeyParameters) asymmetricKeyParameter;
    RSAParameters rsaParameters = new RSAParameters();
    rsaParameters.Modulus = rsaKeyParameters.Modulus.ToByteArrayUnsigned();
    rsaParameters.Exponent = rsaKeyParameters.Exponent.ToByteArrayUnsigned();
    RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
    rsa.ImportParameters(rsaParameters);

    // Get ready to access the Azure Key Vault - any exceptions here are InternalServerError
    var serviceTokenProvider = new AzureServiceTokenProvider();
    var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));

    // Read the secret from the configured endpoint.
    var secret = keyVaultClient.GetSecretAsync(keyVaultEndpoint, keyName).Result;
    string toWrap = secret.Value;

    // Wrap the key
    byte[] keyBytes = Convert.FromBase64String(toWrap);
    byte[] wrappedKey = rsa.Encrypt(keyBytes, false);

    // Back to base64
    string wrappedBase64 = Convert.ToBase64String(wrappedKey);

    // Make the JSON response
    var returnObj = new { wrapped_key = wrappedBase64 };
    var returnJson = JsonConvert.SerializeObject(returnObj);

    log.LogInformation("WrapKey processed successfully.");
    
    return new OkObjectResult(returnJson.ToString());
}

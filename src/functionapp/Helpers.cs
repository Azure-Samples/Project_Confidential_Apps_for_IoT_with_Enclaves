using System.Text.Json;
using functionapp.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace functionapp;

public static class Helpers
{
    public async static Task<Key?> GetKey(this HttpRequest request, string keyName = "", string clientPublicKey = "")
    {
        if (request?.Body == null) return null;

        return request.Method == "POST" ?
            await JsonSerializer.DeserializeAsync<Key>(request.Body) :
            new Key { KeyName = keyName, ClientPublicKeyBase64Encoded = clientPublicKey };
    }

    public static bool IsValid(this Key? key, out List<ValidationResult> validationResults, bool needsPublicKey = false)
    {
        Key k = key ?? new Key();
        ValidationContext context = new ValidationContext(k);
        List<ValidationResult> r = new List<ValidationResult>(); // work around to out param not being assigned CS0177
        Validator.TryValidateObject(k, context, r, true);
        validationResults = r;

        if (!needsPublicKey) return validationResults.Count == 0;

        // check public key has been supplied
        if(string.IsNullOrWhiteSpace(k.ClientPublicKeyBase64Encoded) || k.ClientPublicKey == null)
        {
            validationResults.Add(new ValidationResult("ClientPublicKey is required", new[] { "client_public_key" }));
        }
        else
        {
            try
            {
                _ = Convert.FromBase64String(k.ClientPublicKeyBase64Encoded);
                RSA rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(k.ClientPublicKey, out int bytesRead);
                if (bytesRead == 0) return false;
                _ = rsa.ExportParameters(false);
            }
            catch(Exception exception)
            {
                Console.WriteLine(exception);
                validationResults.Add(new ValidationResult("ClientPublicKey needs to be a valid base64 encoded string of a RSA public key", new[] { "client_public_key" }));
            }

        }

        return validationResults.Count == 0;
    }
}
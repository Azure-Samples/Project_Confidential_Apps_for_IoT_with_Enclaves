using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using functionapp.Models;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;
using Org.BouncyCastle.Security;

namespace functionapp;

public static class Helpers
{
    public async static Task<Key> GetKey(this HttpRequestData request, string keyName = null, string client_public_key = null)
    {
        return request.Method == "POST" ? await JsonSerializer.DeserializeAsync<Key>(request.Body) : new Key { KeyName = keyName, ClientPublicKeyBase64Encoded = client_public_key };
    }

    public async static Task<HttpResponseData> OkObjectResult<T>(this HttpRequestData request, T item)
    {
        HttpResponseData response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(item);
        return response;
    }

    public async static Task<HttpResponseData> BadRequestObjectResult(this HttpRequestData request, List<ValidationResult> errors)
    {
        HttpResponseData response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteAsJsonAsync(errors);
        return response;
    }

    public static bool IsValid(this Key key, out List<ValidationResult> validationResults, bool needsPublicKey = false)
    {
        ValidationContext context = new ValidationContext(key);
        List<ValidationResult> r = new List<ValidationResult>(); // work around to out param not being assigned CS0177
        Validator.TryValidateObject(key, context, r, true);
        validationResults = r;

        if (!needsPublicKey) return validationResults.Count == 0;

        // check public key has been supplied
        if(string.IsNullOrWhiteSpace(key.ClientPublicKeyBase64Encoded))
        {
            validationResults.Add(new ValidationResult("ClientPublicKey is required", new[] { "client_public_key" }));
        }
        else
        {
            try
            {
                _ = Convert.FromBase64String(key.ClientPublicKeyBase64Encoded);
                _ = PublicKeyFactory.CreateKey(key.ClientPublicKey);
            }
            catch
            {
                validationResults.Add(new ValidationResult("ClientPublicKey needs to be a valid base64 encoded string of a SubjectPublicKeyInfo", new[] { "client_public_key" }));
            }

        }

        return validationResults.Count == 0;
    }
}
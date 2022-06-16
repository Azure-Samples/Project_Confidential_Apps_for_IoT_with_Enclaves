using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace functionapp.Models;
public class Key
{
    [JsonPropertyName("key_name")]
    [BindProperty(Name = "key_name", SupportsGet = true)]
    [Newtonsoft.Json.JsonProperty("key_name")]
    [Required]
    [StringLength(36, MinimumLength = 5)]
    public string? KeyName { get; set; }

    [JsonPropertyName("client_public_key")]
    [BindProperty(Name = "client_public_key", SupportsGet = true)]
    [Newtonsoft.Json.JsonProperty("client_public_key")]
    public string? ClientPublicKeyBase64Encoded { get; set; }

    [JsonIgnore]
    public byte[]? ClientPublicKey => !string.IsNullOrWhiteSpace(this.ClientPublicKeyBase64Encoded) ? Convert.FromBase64String(this.ClientPublicKeyBase64Encoded) : default;

    public RSACryptoServiceProvider? GetRSACryptoServiceProvider()
    {
        if (this.ClientPublicKey == null) return null;
        RSA rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(this.ClientPublicKey, out _);
        RSAParameters parameters = rsa.ExportParameters(false);
        RSACryptoServiceProvider provider = new RSACryptoServiceProvider();
        provider.ImportParameters(parameters);
        return provider;
    }
}

public record Response
{
    [JsonPropertyName("message")]
    [BindProperty(Name = "message", SupportsGet = true)]
    public string? Message { get; set; }

    [JsonPropertyName("wrapped_key")]
    [BindProperty(Name = "wrapped_key", SupportsGet = true)]
    [Newtonsoft.Json.JsonProperty("wrapped_key")]
    public string? WrappedKey { get; set; }
}
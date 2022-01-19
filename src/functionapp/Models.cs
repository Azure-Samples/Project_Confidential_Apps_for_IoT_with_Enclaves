using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

namespace functionapp.Models;
public class Key
{
    [JsonPropertyName("key_name")]
    [Required]
    [StringLength(20, MinimumLength = 5)]
    public string? KeyName { get; set; }

    [JsonPropertyName("client_public_key")]
    public string? ClientPublicKeyBase64Encoded { get; set; }

    [JsonIgnore]
    public byte[]? ClientPublicKey => !string.IsNullOrWhiteSpace(this.ClientPublicKeyBase64Encoded) ? Convert.FromBase64String(this.ClientPublicKeyBase64Encoded) : default;

    public RSACryptoServiceProvider? GetRSACryptoServiceProvider()
    {
        if (this.ClientPublicKey == null) return null;
        X509Certificate2? cert = new X509Certificate2(this.ClientPublicKey);
        RSA? rsa = cert?.GetRSAPublicKey();
        if (rsa == null) return null;
        RSAParameters parameters = rsa.ExportParameters(false);
        RSACryptoServiceProvider provider = new RSACryptoServiceProvider();
        provider.ImportParameters(parameters);
        return provider;
    }
}

public record Response
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("wrapped_key")]
    public string? WrappedKey { get; set; }
}
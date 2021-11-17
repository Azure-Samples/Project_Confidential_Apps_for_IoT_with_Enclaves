using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace functionapp.Models;
public class Key
{
    [JsonPropertyName("key_name")]
    [Required]
    [StringLength(20, MinimumLength = 5)]
    public string KeyName { get; set; }

    [JsonPropertyName("client_public_key")]
    public string ClientPublicKeyBase64Encoded { get; set; }

    [JsonIgnore]
    public byte[] ClientPublicKey => !string.IsNullOrWhiteSpace(this.ClientPublicKeyBase64Encoded) ? Convert.FromBase64String(this.ClientPublicKeyBase64Encoded) : default;

    public RSACryptoServiceProvider GetRSACryptoServiceProvider()
    {
        AsymmetricKeyParameter asymmetricKeyParameter = PublicKeyFactory.CreateKey(this.ClientPublicKey);
        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = ((RsaKeyParameters)asymmetricKeyParameter).Modulus.ToByteArrayUnsigned(),
            Exponent = ((RsaKeyParameters)asymmetricKeyParameter).Exponent.ToByteArrayUnsigned()
        });
        return rsa;
    }
}

public class Response
{
    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("wrapped_key")]
    public string WrappedKey { get; set; }
}

public class MyInfo
{
    public MyScheduleStatus ScheduleStatus { get; set; }

    public bool IsPastDue { get; set; }
}

public class MyScheduleStatus
{
    public DateTime Last { get; set; }

    public DateTime Next { get; set; }

    public DateTime LastUpdated { get; set; }
}
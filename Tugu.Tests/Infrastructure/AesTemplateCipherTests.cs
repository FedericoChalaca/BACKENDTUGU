using Microsoft.Extensions.Configuration;
using Tugu.Infrastructure.Security;

namespace Tugu.Tests.Infrastructure;

public class AesTemplateCipherTests
{
    private static AesTemplateCipher NewCipher(string? base64Key = null)
    {
        base64Key ??= Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Biometrics:EncryptionKey"] = base64Key
            })
            .Build();

        return new AesTemplateCipher(config);
    }

    [Fact]
    public void Encrypt_LuegoDecrypt_DevuelveElTemplateOriginal()
    {
        var cipher = NewCipher();
        var original = System.Text.Encoding.UTF8.GetBytes("template-de-prueba-1234567890");

        var encrypted = cipher.Encrypt(original);
        var decrypted = cipher.Decrypt(encrypted);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_NuncaDevuelveElTextoPlano()
    {
        var cipher = NewCipher();
        var original = System.Text.Encoding.UTF8.GetBytes("dato-sensible-biometrico");

        var encrypted = cipher.Encrypt(original);

        Assert.DoesNotContain(
            System.Text.Encoding.UTF8.GetString(original),
            System.Text.Encoding.Latin1.GetString(encrypted));
    }

    [Fact]
    public void Encrypt_DosVecesElMismoTemplate_ProduceBytesDistintos()
    {
        // IV aleatorio por operación: mismo input, salida distinta cada vez.
        var cipher = NewCipher();
        var original = System.Text.Encoding.UTF8.GetBytes("mismo-template");

        var a = cipher.Encrypt(original);
        var b = cipher.Encrypt(original);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ClaveDeLongitudInvalida_LanzaAlConstruir()
    {
        var badKey = Convert.ToBase64String(new byte[16]); // AES-128, no 256

        Assert.Throws<InvalidOperationException>(() => NewCipher(badKey));
    }

    [Fact]
    public void SinClaveConfigurada_LanzaAlConstruir()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => new AesTemplateCipher(config));
    }
}

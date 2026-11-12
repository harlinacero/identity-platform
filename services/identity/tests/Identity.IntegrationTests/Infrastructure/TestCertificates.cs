using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Identity.IntegrationTests.Infrastructure;

public static class TestCertificates
{
    public static string CreateBase64(X509KeyUsageFlags usage)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=identity-tests", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return Convert.ToBase64String(certificate.Export(X509ContentType.Pfx));
    }
}

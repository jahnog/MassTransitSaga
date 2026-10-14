using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Azure.Cosmos;

namespace OrderSaga.Host.Cosmos;

public static class EmulatorCosmos
{
    public static void Apply(CosmosClientOptions options, string endpoint)
    {
        options.ConnectionMode = ConnectionMode.Gateway;
        options.LimitToEndpoint = true;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return;

        if (!uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return;

        options.HttpClientFactory = () =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = AcceptLocalhostCertificate,
            };
            return new HttpClient(handler);
        };
    }

    private static bool AcceptLocalhostCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        return request.RequestUri?.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) == true;
    }
}

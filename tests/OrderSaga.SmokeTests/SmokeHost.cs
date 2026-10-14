using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using OrderSaga.Host;

namespace OrderSaga.SmokeTests;

[SetUpFixture]
public sealed class SmokeHost
{
    private static WebApplication? _app;

    [OneTimeSetUp]
    public async Task Start()
    {
        if (!await PortAnswers("127.0.0.1", 9092) || !await CosmosAnswers())
            Assert.Ignore("start docker compose");

        _app = await OrderSagaHost.Build([], "http://127.0.0.1:5080");
        await _app.StartAsync();
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (_app is null)
            return;

        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static async Task<bool> PortAnswers(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(host, port, timeout.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CosmosAnswers()
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                    request?.RequestUri?.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) == true,
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await http.GetAsync("https://localhost:8081/");
            return true;
        }
        catch
        {
            return false;
        }
    }
}

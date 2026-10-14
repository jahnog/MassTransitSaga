using Microsoft.Playwright;

namespace OrderSaga.SmokeTests;

public sealed class OrderPageSmokeTests
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    [OneTimeSetUp]
    public async Task Browser()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    [OneTimeTearDown]
    public async Task CloseBrowser()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Create_pay_ship_and_deliver_shows_the_committed_facts()
    {
        var page = await _browser.NewPageAsync();
        page.SetDefaultTimeout(60_000);
        await page.GotoAsync("http://127.0.0.1:5080");

        var orderNumber = $"ord-{Guid.NewGuid():N}"[..12];
        await page.FillAsync("#order-number", orderNumber);
        await page.ClickAsync("#create");

        var row = page.Locator($"tr[data-order='{orderNumber}']");
        await Assertions.Expect(row.Locator(".state")).ToHaveTextAsync("Submitted");
        await row.Locator("[data-command=pay]").ClickAsync();
        await Assertions.Expect(row.Locator(".state")).ToHaveTextAsync("Paid");
        await row.Locator("[data-command=ship]").ClickAsync();
        await Assertions.Expect(row.Locator(".state")).ToHaveTextAsync("Shipped");
        await row.Locator("[data-command=deliver]").ClickAsync();
        await Assertions.Expect(row.Locator(".state")).ToHaveTextAsync("Completed");

        await row.ClickAsync();
        await Assertions.Expect(page.Locator("#sent")).ToContainTextAsync("OrderSubmitted");
        await Assertions.Expect(page.Locator("#received")).ToContainTextAsync("OrderAccepted");
        await Assertions.Expect(page.Locator("#received")).ToContainTextAsync("OrderCompleted");
    }
}

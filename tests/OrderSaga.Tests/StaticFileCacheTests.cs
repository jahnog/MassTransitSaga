using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using OrderSaga.Host;

namespace OrderSaga.Tests;

public sealed class StaticFileCacheTests
{
    [Test]
    public void Static_files_revalidate_before_reuse()
    {
        var context = new DefaultHttpContext();
        OrderSagaHost.StaticFiles.OnPrepareResponse!(
            new StaticFileResponseContext(context, new NotFoundFileInfo("site.css")));

        Assert.That(context.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-cache"));
    }
}

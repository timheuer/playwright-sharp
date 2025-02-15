using System.Collections.Generic;
using System.Threading.Tasks;
using PlaywrightSharp.Helpers;
using PlaywrightSharp.Input;
using PlaywrightSharp.Tests.Attributes;
using PlaywrightSharp.Tests.BaseTests;
using PlaywrightSharp.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace PlaywrightSharp.Tests
{
    [Collection(TestConstants.TestFixtureBrowserCollectionName)]
    public class BrowserContextPageEventTests : PlaywrightSharpBrowserBaseTest
    {
        /// <inheritdoc/>
        public BrowserContextPageEventTests(ITestOutputHelper output) : base(output)
        {
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have url")]
        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveUrl()
        {
            await using var context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var (otherPage, _) = await TaskUtils.WhenAll(
                context.WaitForEventAsync(ContextEvent.Page),
                page.EvaluateAsync("url => window.open(url)", TestConstants.EmptyPage));

            Assert.Equal(TestConstants.EmptyPage, otherPage.Page.Url);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have url after domcontentloaded")]
        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveUrlAfterDomcontentloaded()
        {
            await using var context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var (otherPage, _) = await TaskUtils.WhenAll(
                context.WaitForEventAsync(ContextEvent.Page),
                page.EvaluateAsync("url => window.open(url)", TestConstants.EmptyPage));

            await otherPage.Page.WaitForLoadStateAsync(LifecycleEvent.DOMContentLoaded);
            Assert.Equal(TestConstants.EmptyPage, otherPage.Page.Url);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have about:blank url with domcontentloaded")]
        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveAboutBlankUrlWithDomcontentloaded()
        {
            await using var context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var (otherPage, _) = await TaskUtils.WhenAll(
                context.WaitForEventAsync(ContextEvent.Page),
                page.EvaluateAsync("url => window.open(url)", "about:blank"));

            await otherPage.Page.WaitForLoadStateAsync(LifecycleEvent.DOMContentLoaded);
            Assert.Equal("about:blank", otherPage.Page.Url);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have about:blank for empty url with domcontentloaded")]
        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveAboutBlankUrlForEmptyUrlWithDomcontentloaded()
        {
            await using var context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var (otherPage, _) = await TaskUtils.WhenAll(
                context.WaitForEventAsync(ContextEvent.Page),
                page.EvaluateAsync("() => window.open()"));

            await otherPage.Page.WaitForLoadStateAsync(LifecycleEvent.DOMContentLoaded);
            Assert.Equal("about:blank", otherPage.Page.Url);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should report when a new page is created and closed")]
        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportWhenANewPageIsCreatedAndClosed()
        {
            await using var context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var (otherPageEvent, _) = await TaskUtils.WhenAll(
                context.WaitForEventAsync(ContextEvent.Page),
                page.EvaluateAsync("url => window.open(url)", TestConstants.CrossProcessUrl + "/empty.html"));
            var otherPage = otherPageEvent.Page;

            Assert.Contains(TestConstants.CrossProcessUrl, otherPage.Url);
            Assert.Equal("Hello world", await otherPage.EvaluateAsync<string>("() => ['Hello', 'world'].join(' ')"));
            Assert.NotNull(await otherPage.QuerySelectorAsync("body"));


            var allPages = context.Pages;
            Assert.Contains(page, allPages);
            Assert.Contains(otherPage, allPages);

            var closeEventReceived = new TaskCompletionSource<bool>();
            otherPage.Close += (_, _) => closeEventReceived.TrySetResult(true);

            await otherPage.CloseAsync();
            await closeEventReceived.Task.WithTimeout(TestConstants.DefaultTaskTimeout);

            allPages = context.Pages;
            Assert.Contains(page, allPages);
            Assert.DoesNotContain(otherPage, allPages);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should report initialized pages")]
        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportInitializedPages()
        {
            await using var context = await Browser.NewContextAsync();
            var pageTask = context.WaitForEventAsync(ContextEvent.Page);
            _ = context.NewPageAsync();
            var newPage = await pageTask;
            Assert.Equal("about:blank", newPage.Page.Url);

            var popupTask = context.WaitForEventAsync(ContextEvent.Page);
            var evaluateTask = newPage.Page.EvaluateAsync("() => window.open('about:blank')");
            var popup = await popupTask;
            Assert.Equal("about:blank", popup.Page.Url);
            await evaluateTask;
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should not crash while redirecting of original request was missed")]
        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashWhileRedirectingOfOriginalRequestWasMissed()
        {
            await using var context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            Server.SetRoute("/one-style.css", context =>
            {
                context.Response.Redirect("/one-style.css");
                return Task.CompletedTask;
            });

            // Open a new page. Use window.open to connect to the page later.
            var pageCreatedTask = context.WaitForEventAsync(ContextEvent.Page);
            await TaskUtils.WhenAll(
                pageCreatedTask,
                page.EvaluateAsync("url => window.open(url)", TestConstants.ServerUrl + "/one-style.html"),
                Server.WaitForRequest("/one-style.css"));

            var newPage = pageCreatedTask.Result.Page;

            await newPage.WaitForLoadStateAsync(LifecycleEvent.DOMContentLoaded);
            Assert.Equal(TestConstants.ServerUrl + "/one-style.html", newPage.Url);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have an opener")]
        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveAnOpener()
        {
            await using var context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);

            var (popupEvent, _) = await TaskUtils.WhenAll(
              context.WaitForEventAsync(ContextEvent.Page),
              page.GoToAsync(TestConstants.ServerUrl + "/popup/window-open.html"));

            var popup = popupEvent.Page;
            Assert.Equal(TestConstants.ServerUrl + "/popup/popup.html", popup.Url);
            Assert.Same(page, await popup.GetOpenerAsync());
            Assert.Null(await page.GetOpenerAsync());
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should fire page lifecycle events")]
        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldFirePageLifecycleEvents()
        {
            await using var context = await Browser.NewContextAsync();
            var events = new List<string>();

            context.Page += (_, e) =>
            {
                events.Add("CREATED: " + e.Page.Url);
                e.Page.Close += (sender, _) => events.Add("DESTROYED: " + ((IPage)sender).Url);
            };

            var page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);
            await page.CloseAsync();
            Assert.Equal(
                new List<string>()
                {
                    "CREATED: about:blank",
                    $"DESTROYED: {TestConstants.EmptyPage}"
                },
                events);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should work with Shift-clicking")]
        [SkipBrowserAndPlatformFact(skipWebkit: true)]
        public async Task ShouldWorkWithShiftClicking()
        {
            // WebKit: Shift+Click does not open a new window.
            await using var context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);
            await page.SetContentAsync("<a href=\"/one-style.html\">yo</a>");

            var popupEventTask = context.WaitForEventAsync(ContextEvent.Page);
            await TaskUtils.WhenAll(
              popupEventTask,
              page.ClickAsync("a", modifiers: new[] { Modifier.Shift }));

            Assert.Null(await popupEventTask.Result.Page.GetOpenerAsync());
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should report when a new page is created and closed")]
        [SkipBrowserAndPlatformFact(skipWebkit: true, skipFirefox: true)]
        public async Task ShouldWorkWithCtrlClicking()
        {
            // Firefox: reports an opener in this case.
            // WebKit: Ctrl+Click does not open a new tab.
            await using var context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);
            await page.SetContentAsync("<a href=\"/one-style.html\">yo</a>");

            var popupEventTask = context.WaitForEventAsync(ContextEvent.Page);
            await TaskUtils.WhenAll(
              popupEventTask,
              page.ClickAsync("a", modifiers: new[] { TestConstants.IsMacOSX ? Modifier.Meta : Modifier.Control }));

            Assert.Null(await popupEventTask.Result.Page.GetOpenerAsync());
        }
    }
}

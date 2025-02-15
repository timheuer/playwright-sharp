using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.Helpers;
using PlaywrightSharp.Tests.Attributes;
using PlaywrightSharp.Tests.BaseTests;
using PlaywrightSharp.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace PlaywrightSharp.Tests.Chromium
{
    [Collection(TestConstants.TestFixtureBrowserCollectionName)]
    public class SessionTests : PlaywrightSharpPageBaseTest
    {
        /// <inheritdoc/>
        public SessionTests(ITestOutputHelper output) : base(output)
        {
        }

        [PlaywrightTest("chromium/session.spec.ts", "session", "should work")]
        [SkipBrowserAndPlatformFact(skipFirefox: true, skipWebkit: true)]
        public async Task ShouldWork()
        {
            var client = await ((IChromiumBrowserContext)Page.Context).NewCDPSessionAsync(Page);

            await TaskUtils.WhenAll(
              client.SendAsync("Runtime.enable"),
              client.SendAsync("Runtime.evaluate", new { Expression = "window.foo = 'bar'" })
            );
            string foo = await Page.EvaluateAsync<string>("window.foo");
            Assert.Equal("bar", foo);
        }

        [PlaywrightTest("chromium/session.spec.ts", "session", "should send events")]
        [SkipBrowserAndPlatformFact(skipFirefox: true, skipWebkit: true)]
        public async Task ShouldSendEvents()
        {
            var client = await ((IChromiumBrowserContext)Page.Context).NewCDPSessionAsync(Page);

            await client.SendAsync("Network.enable");
            var events = new List<CDPEventArgs>();

            client.MessageReceived += (_, e) =>
            {
                if (e.Method == "Network.requestWillBeSent")
                {
                    events.Add(e);
                }
            };

            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.Single(events);
        }

        [PlaywrightTest("chromium/session.spec.ts", "session", "should enable and disable domains independently")]
        [SkipBrowserAndPlatformFact(skipFirefox: true, skipWebkit: true)]
        public async Task ShouldEnableAndDisableDomainsIndependently()
        {
            var client = await ((IChromiumBrowserContext)Page.Context).NewCDPSessionAsync(Page);
            await client.SendAsync("Runtime.enable");
            await client.SendAsync("Debugger.enable");
            // JS coverage enables and then disables Debugger domain.
            await Page.Coverage.StartJSCoverageAsync();
            await Page.Coverage.StopJSCoverageAsync();
            // generate a script in page and wait for the event.
            var tcs = new TaskCompletionSource<bool>();

            client.MessageReceived += (_, e) =>
            {
                if (e.Method == "Debugger.scriptParsed" && e.Params?.GetProperty("url").GetString() == "foo.js")
                {
                    tcs.TrySetResult(true);
                }
            };

            await TaskUtils.WhenAll(
                tcs.Task,
                Page.EvaluateAsync("//# sourceURL=foo.js"));
        }

        [PlaywrightTest("chromium/session.spec.ts", "session", "should be able to detach session")]
        [SkipBrowserAndPlatformFact(skipFirefox: true, skipWebkit: true)]
        public async Task ShouldBeAbleToDetachSession()
        {
            var client = await ((IChromiumBrowserContext)Page.Context).NewCDPSessionAsync(Page);
            await client.SendAsync("Runtime.enable");
            var evalResponse = await client.SendAsync("Runtime.evaluate", new
            {
                Expression = "1 + 2",
                ReturnByValue = true
            });
            Assert.Equal(3, evalResponse?.GetProperty("result").GetProperty("result").GetProperty("value").GetInt32());
            await client.DetachAsync();

            var exception = await Assert.ThrowsAnyAsync<Exception>(()
                => client.SendAsync("Runtime.evaluate", new
                {
                    Expression = "3 + 1",
                    ReturnByValue = true
                }));
            Assert.Contains(DriverMessages.BrowserOrContextClosedExceptionMessage, exception.Message);
        }

        [PlaywrightTest("chromium/session.spec.ts", "session", "should throw nice errors")]
        [SkipBrowserAndPlatformFact(skipFirefox: true, skipWebkit: true)]
        public async Task ShouldThrowNiceErrors()
        {
            var client = await ((IChromiumBrowserContext)Page.Context).NewCDPSessionAsync(Page);
            async Task TheSourceOfTheProblems() => await client.SendAsync("ThisCommand.DoesNotExist");

            var exception = await Assert.ThrowsAsync<PlaywrightSharpException>(async () =>
            {
                await TheSourceOfTheProblems();
            });
            Assert.Contains("TheSourceOfTheProblems", exception.StackTrace);
            Assert.Contains("ThisCommand.DoesNotExist", exception.Message);
        }

        [PlaywrightTest("chromium/session.spec.ts", "session", "should not break page.close()")]
        [SkipBrowserAndPlatformFact(skipFirefox: true, skipWebkit: true)]
        public async Task ShouldNotBreakPageClose()
        {
            var session = await ((IChromiumBrowserContext)Page.Context).NewCDPSessionAsync(Page);
            await session.DetachAsync();
            await Page.CloseAsync();
        }

        [PlaywrightTest("chromium/session.spec.ts", "session", "should detach when page closes")]
        [SkipBrowserAndPlatformFact(skipFirefox: true, skipWebkit: true)]
        public async Task ShouldDetachWhenPageCloses()
        {
            var session = await ((IChromiumBrowserContext)Page.Context).NewCDPSessionAsync(Page);
            await Page.CloseAsync();
            await Assert.ThrowsAnyAsync<PlaywrightSharpException>(() => session.DetachAsync());
        }

        [PlaywrightTest("chromium/session.spec.ts", "session", "should work with newBrowserCDPSession")]
        [SkipBrowserAndPlatformFact(skipFirefox: true, skipWebkit: true)]
        public async Task ShouldWorkWithNewBrowserCDPSession()
        {
            var session = await ((IChromiumBrowser)Browser).NewBrowserCDPSessionAsync();
            Assert.NotNull(await session.SendAsync<object>("Browser.getVersion"));

            bool gotEvent = false;
            session.MessageReceived += (_, e) =>
            {
                if (e.Method == "Target.targetCreated")
                {
                    gotEvent = true;
                }
            };

            await session.SendAsync("Target.setDiscoverTargets", new { discover = true });
            Assert.True(gotEvent);
            await session.DetachAsync();
        }
    }
}

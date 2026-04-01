using System;
using System.Net;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Web
{
    public class WebFetchServiceTests : TestBase
    {
        [Fact]
        public void WebFetchService_StaticHelpers_Work()
        {
            // WebFetchService.ExtractTitle is internal
            // WebFetchService.HtmlToText is internal
            // WebFetchService.DecodeEntities is internal
            
            string html = "<html><head><title>Example &amp; Test</title></head><body><h1>Hello</h1><p>One&nbsp;two</p></body></html>";
            
            Assert.Equal("Example & Test", WebFetchService.ExtractTitle(html));
            
            string text = WebFetchService.HtmlToText(html);
            Assert.Contains("Hello", text);
            Assert.Contains("One two", text);
            
            Assert.Equal("Fish & Chips", WebFetchService.DecodeEntities("Fish &amp; Chips"));
        }

        [Fact]
        public void WebFetchResult_ToToolResponseString_FormatsSuccessAndErrors()
        {
            WebFetchResult result = new WebFetchResult
            {
                Url = "https://example.com",
                Title = "Example",
                Text = "Hello world",
                Truncated = true,
                MaxChars = 100
            };
            
            string output = result.ToToolResponseString();
            Assert.Contains("URL: https://example.com", output);
            Assert.Contains("Title: Example", output);
            Assert.Contains("Hello world", output);
            Assert.Contains("truncated", output, StringComparison.OrdinalIgnoreCase);

            WebFetchResult error = WebFetchResult.Error("boom", "https://example.com");
            Assert.Equal("FAILED: boom", error.ToToolResponseString());
        }
    }
}

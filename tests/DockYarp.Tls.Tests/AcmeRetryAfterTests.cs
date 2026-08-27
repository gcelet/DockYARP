namespace DockYarp.Tls.Tests;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AwesomeAssertions;

using DockYarp.Tls.Acme;

/// <summary>Tests for <see cref="AcmeRetryAfter"/> — confirms <see cref="HttpResponseHeaders.RetryAfter"/>
/// actually parses both RFC 7231 §7.1.3 forms as expected (real behavior, not assumed), plus the cap.</summary>
public sealed class AcmeRetryAfterTests
{
    [Test]
    public void Parse_WithDelaySecondsForm_ReturnsThatDuration()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

        AcmeRetryAfter.Parse(response.Headers).Should().Be(TimeSpan.FromSeconds(30));
    }

    [Test]
    public void Parse_WithHttpDateForm_ReturnsRemainingDuration()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(30));

        TimeSpan? parsed = AcmeRetryAfter.Parse(response.Headers);
        parsed.Should().NotBeNull();
        parsed.Value.Should().BeCloseTo(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2), "clock precision between construction and parsing");
    }

    [Test]
    public void Parse_WithHttpDateInThePast_ClampsToZero()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(-30));

        AcmeRetryAfter.Parse(response.Headers).Should().Be(TimeSpan.Zero);
    }

    [Test]
    public void Parse_WithNoHeader_ReturnsNull()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);

        AcmeRetryAfter.Parse(response.Headers).Should().BeNull();
    }

    [Test]
    public void Capped_ValueBelowCap_ReturnsAsIs()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));

        AcmeRetryAfter.Capped(response.Headers).Should().Be(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void Capped_ValueAboveCap_ReturnsTheCap()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));

        AcmeRetryAfter.Capped(response.Headers).Should().Be(AcmeRetryAfter.Cap);
    }

    [Test]
    public void Capped_WithNoHeader_ReturnsNull()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);

        AcmeRetryAfter.Capped(response.Headers).Should().BeNull();
    }
}

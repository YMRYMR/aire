using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public class ProviderAdapterApplicationServiceTests
{
    private sealed class FakeProviderAdapter(string providerType) : IProviderAdapter
    {
        public string ProviderType { get; } = providerType;

        public bool CanHandle(string providerType)
            => string.Equals(ProviderType, providerType, StringComparison.OrdinalIgnoreCase);

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request) => null;

        public Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext)
            => Task.FromResult(ProviderExecutionResult.Succeeded(WorkflowIntent.AssistantText("ok")));

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(new ProviderSmokeTestResult(true, null));

        public Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(ProviderValidationOutcome.Valid());
    }

    [Fact]
    public void Resolve_ReturnsMatchingAdapter_IgnoringCase()
    {
        ProviderAdapterApplicationService service = new(
        [
            new FakeProviderAdapter("OpenAI"),
            new FakeProviderAdapter("Codex")
        ]);

        IProviderAdapter adapter = service.Resolve("codex");

        Assert.Equal("Codex", adapter.ProviderType);
    }

    [Fact]
    public void TryResolve_ReturnsNull_WhenNoAdapterMatches()
    {
        ProviderAdapterApplicationService service = new(
        [
            new FakeProviderAdapter("OpenAI")
        ]);

        Assert.Null(service.TryResolve("Ollama"));
    }

    [Fact]
    public void Resolve_Throws_WhenNoAdapterMatches()
    {
        ProviderAdapterApplicationService service = new(
        [
            new FakeProviderAdapter("OpenAI")
        ]);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => service.Resolve("Ollama"));
        Assert.Contains("Ollama", ex.Message, StringComparison.Ordinal);
    }
}

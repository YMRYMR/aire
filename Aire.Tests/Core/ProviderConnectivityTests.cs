using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Core;

public class ProviderConnectivityTests
{
    private static bool IsEnabled => string.Equals(Environment.GetEnvironmentVariable("AIRE_RUN_CONNECTIVITY_TESTS"), "1", StringComparison.Ordinal);

    [Fact]
    public async Task ValidateAllEnabledProviders()
    {
        if (!IsEnabled)
        {
            return;
        }
        DatabaseService db = new DatabaseService();
        await db.InitializeAsync();
        ProviderFactory factory = new ProviderFactory(db);
        List<Provider> enabledProviders = (await factory.GetConfiguredProvidersAsync()).Where((Provider p) => p.IsEnabled).ToList();
        Console.WriteLine($"Found {enabledProviders.Count} enabled provider(s).");
        List<(string Name, string Type, bool HasApiKey, bool ValidationSuccess, string? Error)> results = new List<(string, string, bool, bool, string)>();
        foreach (Provider providerConfig in enabledProviders)
        {
            Console.WriteLine($"Testing provider: {providerConfig.Name} ({providerConfig.Type})");
            try
            {
                IAiProvider provider = factory.CreateProvider(providerConfig);
                bool hasApiKey = !string.IsNullOrEmpty(providerConfig.ApiKey);
                bool validationSuccess = false;
                string error = null;
                if (hasApiKey)
                {
                    try
                    {
                validationSuccess = (await provider.ValidateConfigurationAsync()).IsValid;
                        if (!validationSuccess)
                        {
                            error = "Validation returned false (likely invalid API key or network issue)";
                        }
                    }
                    catch (Exception ex)
                    {
                        Exception ex2 = ex;
                        validationSuccess = false;
                        error = ex2.Message;
                    }
                }
                else
                {
                    error = "No API key configured";
                }
                results.Add((providerConfig.Name, providerConfig.Type, hasApiKey, validationSuccess, error));
                Console.WriteLine($"  Has API key: {hasApiKey}");
                Console.WriteLine($"  Validation success: {validationSuccess}");
                if (error != null)
                {
                    Console.WriteLine("  Error: " + error);
                }
            }
            catch (Exception ex3)
            {
                Console.WriteLine("  Failed to create or validate provider: " + ex3.Message);
                results.Add((providerConfig.Name, providerConfig.Type, false, false, ex3.Message));
            }
        }
        Console.WriteLine("\n=== Provider Connectivity Report ===");
        foreach (var r in results)
        {
            Console.WriteLine(r.Name + " (" + r.Type + "):");
            Console.WriteLine($"  API key configured: {r.HasApiKey}");
            Console.WriteLine($"  Validation passed: {r.ValidationSuccess}");
            if (r.Error != null)
            {
                Console.WriteLine("  Error: " + r.Error);
            }
            Console.WriteLine();
        }
        List<(string Name, string Type, bool HasApiKey, bool ValidationSuccess, string Error)> failures = results.Where(((string Name, string Type, bool HasApiKey, bool ValidationSuccess, string Error) tuple) => tuple.HasApiKey && !tuple.ValidationSuccess).ToList();
        if (failures.Any())
        {
            string failureNames = string.Join(", ", failures.Select(((string Name, string Type, bool HasApiKey, bool ValidationSuccess, string Error) f) => f.Name));
            Assert.Fail("The following providers failed validation: " + failureNames);
        }
    }
}

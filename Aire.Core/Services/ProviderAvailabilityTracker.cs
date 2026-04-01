using System.Collections.Concurrent;

namespace Aire.Services
{
    /// <summary>
    /// Why a provider is in cooldown.
    /// </summary>
    public enum CooldownReason
    {
        None,
        RateLimit,
        QuotaExhausted,
        BillingError,
        ServiceUnavailable
    }

    public sealed class CooldownEntry
    {
        public CooldownReason Reason { get; init; }
        public DateTime Until { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// Tracks per-provider cooldown windows in memory.
    /// Thread-safe; raises <see cref="AvailabilityChanged"/> on the calling thread.
    /// </summary>
    public sealed class ProviderAvailabilityTracker
    {
        public static readonly ProviderAvailabilityTracker Instance = new();

        private readonly ConcurrentDictionary<int, CooldownEntry> _cooldowns = new();

        public event Action<int>? AvailabilityChanged;

        private ProviderAvailabilityTracker()
        {
        }

        private static TimeSpan DurationFor(CooldownReason reason) => reason switch
        {
            CooldownReason.RateLimit => TimeSpan.FromMinutes(5),
            CooldownReason.QuotaExhausted => TimeSpan.FromHours(6),
            CooldownReason.BillingError => TimeSpan.MaxValue,
            CooldownReason.ServiceUnavailable => TimeSpan.FromMinutes(30),
            _ => TimeSpan.Zero
        };

        public void SetCooldown(int providerId, CooldownReason reason, string message = "")
        {
            if (reason == CooldownReason.None)
                return;

            var duration = DurationFor(reason);
            var until = duration == TimeSpan.MaxValue
                ? DateTime.MaxValue
                : DateTime.UtcNow + duration;

            _cooldowns[providerId] = new CooldownEntry
            {
                Reason = reason,
                Until = until,
                Message = message
            };

            AvailabilityChanged?.Invoke(providerId);
        }

        public void ClearCooldown(int providerId)
        {
            if (_cooldowns.TryRemove(providerId, out _))
                AvailabilityChanged?.Invoke(providerId);
        }

        public bool IsOnCooldown(int providerId)
        {
            if (!_cooldowns.TryGetValue(providerId, out var entry))
                return false;

            if (entry.Until > DateTime.UtcNow)
                return true;

            _cooldowns.TryRemove(providerId, out _);
            return false;
        }

        public CooldownEntry? GetCooldown(int providerId)
        {
            if (!_cooldowns.TryGetValue(providerId, out var entry))
                return null;

            if (entry.Until > DateTime.UtcNow)
                return entry;

            _cooldowns.TryRemove(providerId, out _);
            return null;
        }
    }
}

namespace Aire.Providers
{
    public class TokenUsage
    {
        public long Used { get; set; }
        public long? Limit { get; set; }
        public DateTime? ResetDate { get; set; }
        public string Unit { get; set; } = "tokens";
        public long? Remaining => Limit.HasValue ? Limit.Value - Used : null;
        public double Percentage => Limit.HasValue && Limit.Value > 0 ? (double)Used / Limit.Value : 0;
        public bool IsLimitReached => Limit.HasValue && Used >= Limit.Value;
    }
}

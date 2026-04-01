using Aire.Services.Email;

namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Persistence boundary for email account configuration.
    /// </summary>
    public interface IEmailAccountRepository
    {
        Task<List<EmailAccount>> GetEmailAccountsAsync();
        Task<int> InsertEmailAccountAsync(EmailAccount account);
        Task UpdateEmailAccountAsync(EmailAccount account);
        Task DeleteEmailAccountAsync(int id);
    }
}

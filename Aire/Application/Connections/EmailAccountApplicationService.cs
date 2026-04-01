using Aire.AppLayer.Abstractions;
using Aire.Services.Email;

namespace Aire.AppLayer.Connections
{
    /// <summary>
    /// Application-layer use cases for managing email account configuration in the Settings UI.
    /// </summary>
    public sealed class EmailAccountApplicationService
    {
        private readonly IEmailAccountRepository _emailAccounts;

        /// <summary>
        /// Creates the service over the email account persistence boundary.
        /// </summary>
        /// <param name="emailAccounts">Repository used to persist email account definitions.</param>
        public EmailAccountApplicationService(IEmailAccountRepository emailAccounts)
        {
            _emailAccounts = emailAccounts;
        }

        /// <summary>Loads all configured email accounts.</summary>
        public Task<List<EmailAccount>> GetEmailAccountsAsync()
            => _emailAccounts.GetEmailAccountsAsync();

        /// <summary>Inserts a new email account and returns its generated id.</summary>
        public Task<int> InsertEmailAccountAsync(EmailAccount account)
            => _emailAccounts.InsertEmailAccountAsync(account);

        /// <summary>Updates an existing email account.</summary>
        public Task UpdateEmailAccountAsync(EmailAccount account)
            => _emailAccounts.UpdateEmailAccountAsync(account);

        /// <summary>Deletes an email account by id.</summary>
        public Task DeleteEmailAccountAsync(int id)
            => _emailAccounts.DeleteEmailAccountAsync(id);
    }
}

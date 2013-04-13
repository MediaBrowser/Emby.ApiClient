using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Class AuthenticationExtensions
    /// </summary>
    public static class AuthenticationExtensions
    {
        /// <summary>
        /// Authenticates a user and returns the result
        /// This has to be an extenson because the Cryptography classes are not available in portable class libraries
        /// </summary>
        /// <param name="apiClient">The API client.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="password">The password.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public static Task AuthenticateUserAsync(this ApiClient apiClient, string userId, string password)
        {
            using (var provider = SHA1.Create())
            {
                var hash = provider.ComputeHash(Encoding.UTF8.GetBytes(password ?? string.Empty));

                return apiClient.AuthenticateUserAsync(userId, hash);
            }
        }
    }
}

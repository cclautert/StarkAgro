using AgripeWebAPI.Models.Interfaces;
using BCrypt.Net;

namespace AgripeWebAPI.Services
{
    public class PasswordHasherService : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or empty.", nameof(password));

            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            if (string.IsNullOrWhiteSpace(hashedPassword))
                return false;

            // Check if the stored password is a BCrypt hash (starts with $2a$, $2b$, $2x$, or $2y$)
            if (IsBcryptHash(hashedPassword))
            {
                try
                {
                    return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
                }
                catch (SaltParseException)
                {
                    // Invalid BCrypt hash format
                    return false;
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                // Legacy plain text password - compare directly
                // This allows migration from plain text to hashed passwords
                return password == hashedPassword;
            }
        }

        /// <summary>
        /// Checks if a string is a valid BCrypt hash format
        /// </summary>
        private bool IsBcryptHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return false;

            // BCrypt hashes start with $2a$, $2b$, $2x$, or $2y$ followed by $ and cost parameter
            return hash.StartsWith("$2a$") || 
                   hash.StartsWith("$2b$") || 
                   hash.StartsWith("$2x$") || 
                   hash.StartsWith("$2y$");
        }
    }
}

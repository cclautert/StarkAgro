using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AgripeWebAPI.Validators
{
    public class EmailAttribute : ValidationAttribute
    {
        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return false;

            var email = value.ToString()!;
            return EmailRegex.IsMatch(email) && email.Length <= 254; // RFC 5321 limit
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} must be a valid email address.";
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace StarkAgroAPI.Validators
{
    public class PasswordStrengthAttribute : ValidationAttribute
    {
        private const int MinLength = 8;
        private const int MaxLength = 100;

        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return false;

            var password = value.ToString()!;

            if (password.Length < MinLength || password.Length > MaxLength)
                return false;

            // At least one uppercase letter
            if (!Regex.IsMatch(password, @"[A-Z]"))
                return false;

            // At least one lowercase letter
            if (!Regex.IsMatch(password, @"[a-z]"))
                return false;

            // At least one digit
            if (!Regex.IsMatch(password, @"[0-9]"))
                return false;

            // At least one special character
            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
                return false;

            return true;
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} must be between {MinLength} and {MaxLength} characters long and contain at least one uppercase letter, one lowercase letter, one digit, and one special character.";
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AgripeWebAPI.Validators
{
    public class MacAddressAttribute : ValidationAttribute
    {
        private static readonly Regex MacRegex = new Regex(
            @"^([0-9A-F]{2}:){5}[0-9A-F]{2}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public override bool IsValid(object? value)
        {
            if (value == null)
                return true; // null is allowed; use [Required] to reject null

            if (string.IsNullOrWhiteSpace(value.ToString()))
                return false;

            var normalised = value.ToString()!.ToUpperInvariant();
            return MacRegex.IsMatch(normalised);
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} must be a valid MAC address in the format XX:XX:XX:XX:XX:XX.";
        }
    }
}

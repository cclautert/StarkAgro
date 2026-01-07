using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Validators;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class EditUserRequest : IRequest<EditUserResponse>
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Email(ErrorMessage = "Invalid email format")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
        public string Email { get; set; }

        // Password is optional for edit - only required if changing password
        // Validation is handled in the handler to allow optional password updates
        public string? Password { get; set; }

        public int CurrentUserId { get; set; } // Set by controller from JWT claims
    }
}

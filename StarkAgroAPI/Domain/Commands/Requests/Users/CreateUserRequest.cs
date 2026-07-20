using StarkAgroAPI.Domain.Commands.Responses.Users;
using StarkAgroAPI.Validators;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Commands.Requests.Users
{
    public class CreateUserRequest : IRequest<CreateUserResponse>
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Email(ErrorMessage = "Invalid email format")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [PasswordStrength(ErrorMessage = "Password does not meet strength requirements")]
        public string Password { get; set; }
    }
}

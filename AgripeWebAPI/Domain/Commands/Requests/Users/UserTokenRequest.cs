using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Validators;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class UserTokenRequest : IRequest<UserTokenResponse>
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Email(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
    }
}

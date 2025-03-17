using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace SDVA.Models
{
    public class LoginModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "The email address is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string UserEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        //[StringLength(100, MinimumLength = 6, ErrorMessage = "The password must be at least 6 characters long.")]
        public string Password { get; set; }
    }

}
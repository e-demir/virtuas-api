﻿using System;
namespace fazz.Models.Requests
{
	public class RegisterRequest
	{
		public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Role { get; set; } = "Customer";
    }
}


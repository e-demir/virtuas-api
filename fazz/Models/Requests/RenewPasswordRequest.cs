using System;

namespace fazz.Models.Requests
{
    public class RenewPasswordRequest
    {
        public string NewPassword { get; set; }     

        public string Username { get; set; }        
    }
}

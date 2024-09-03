using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using fazz.Models.Entities;
using fazz.Models.Requests;
using fazz.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace fazz.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;

        public AuthController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult Ping()
        {
            return Ok("Pong");
        }

        [HttpPost]
        public IActionResult Login(LoginRequest request)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");
            int clinicId = 0;

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var firstPasswordQuery =
                    "select isFirstPassword from users where username = @username and role='clinic'";
                var isFirstPassword = connection.QueryFirstOrDefault<bool>(
                    firstPasswordQuery,
                    new { username = request.Username }
                );
                if (isFirstPassword)
                {
                    return Unauthorized("Change First Password");
                }
                var query =
                    "SELECT * FROM users WHERE username = @username AND password = @password";
                var user = connection.QueryFirstOrDefault<User>(
                    query,
                    new { username = request.Username, password = request.Password }
                );

                if (user == null)
                {
                    return Unauthorized(new LoginResponse { IsSuccessful = false, Role = "" }); // Kullanıcı bulunamazsa 401 döndür
                }
                if (user.Role == "clinic")
                {
                    var query2 = "SELECT id FROM clinics WHERE userId = @userId";
                    clinicId = connection.QueryFirstOrDefault<int>(
                        query2,
                        new { userId = user.Id }
                    );
                }

                return Ok(
                    new LoginResponse
                    {
                        IsSuccessful = true,
                        Role = user.Role,
                        Username = user.Name,
                        Id = user.Id,
                        ClinicId = clinicId
                    }
                );
            }
        }

        [HttpPost]
        public IActionResult RenewPassword(RenewPasswordRequest request)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");
           
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    var updateQuery =
                        "update users set password=@password, isFirstPassword=0 where username = @username";
                    var isFirstPassword = connection.QueryFirstOrDefault<int>(
                        updateQuery,
                        new { password = request.NewPassword, username = request.Username }
                    );
                    if (isFirstPassword == 0)
                    return Ok("Successfully changed");
                    else
                    return StatusCode(500);
                }
            }
            catch (Exception ex) {
                return StatusCode(500,ex.Message);
             }
        }

        [HttpPost]
        public IActionResult Register(RegisterRequest request)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM users WHERE email = @Email";
                var existingUser = connection.QueryFirstOrDefault<User>(
                    query,
                    new { Email = request.Email }
                );

                if (existingUser != null)
                {
                    return Conflict(new { Message = "Email already in use" }); // Email zaten kullanılıyorsa 409 döndür
                }

                var user = new User
                {
                    Name = request.Name,
                    Surname = request.Surname,
                    Email = request.Email,
                    Password = request.Password, // Parolayı hash'lemeyi düşünün
                    Role = request.Role,
                    PhoneNumber = request.PhoneNumber
                };

                var query2 =
                    "INSERT INTO users (name, surname, email, password, role,phoneNumber) VALUES (@Name, @Surname, @Email, @Password, @Role, @PhoneNumber)";
                var result = connection.Execute(query2, user);
                var isCreated = result > 0;

                if (!isCreated)
                {
                    return StatusCode(500, new { Message = "User could not be created" }); // Kullanıcı oluşturulamazsa 500 döndür
                }

                return Ok(new { Message = "User created successfully" });
            }
        }
    }
}

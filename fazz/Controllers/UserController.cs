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

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace fazz.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IConfiguration _config;

        public UserController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult Ping()
        {
            return Ok("Pong");
        }

        [HttpPost]
        public IActionResult Profile(int userId)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM users WHERE id=@UserId";
                var user = connection.QueryFirstOrDefault<User>(query, new { userId = userId });

                if (user == null)
                {
                    return Unauthorized(new LoginResponse { IsSuccessful = false, Role = "" }); // Kullanıcı bulunamazsa 401 döndür
                }

                return Ok(new UserResponse {Email = user.Email, Name = user.Name, Surname = user.Surname, PhoneNumber = user.PhoneNumber, Password = user.Password, });
            }
        }


        [HttpPost]
        public IActionResult Update(UserRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            var propfileUpdate = new User
            {
                 Id=request.Id,
                 Email=request.Email,
                 Name=request.Name,
                 Surname=request.Surname,
                 PhoneNumber=request.PhoneNumber,
                       
            };

            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();




                var query = "UPDATE users SET name = @Name, surname= @Surname, phoneNumber=@PhoneNumber,email=@Email  WHERE id = @Id";

                var result = connection.Execute(query, propfileUpdate);

                if (result > 0)
                {
                    return Ok(new UserResponse { Email = request.Email, Name = request.Name, Surname = request.Surname, PhoneNumber = request.PhoneNumber });
                }
                else
                {
                    return NotFound();
                }
           
            }
        }




    [HttpGet]
        public IActionResult UserDetails()
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT u.id,u.name,u.surname,u.email,u.phoneNumber, COUNT(app.id) AS ApplicationCount FROM users u LEFT JOIN applications app ON app.userId = u.id WHERE u.role = 'client' GROUP BY u.id";
                var user = connection.Query<DetailUser>(query).ToList();

                if (user == null)
                {
                    return Unauthorized(new LoginResponse { IsSuccessful = false, Role = "" }); // Kullanıcı bulunamazsa 401 döndür
                }

                return Ok(user);
            }
        }

        [HttpGet]
        public IActionResult UserApplications()
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT u.id,u.name,u.surname,u.email,u.phoneNumber, COUNT(app.id) AS ApplicationCount FROM users u LEFT JOIN applications app ON app.userId = u.id WHERE u.role = 'client' GROUP BY u.id";
                var user = connection.Query<DetailUser>(query).ToList();

                if (user == null)
                {
                    return Unauthorized(new LoginResponse { IsSuccessful = false, Role = "" }); // Kullanıcı bulunamazsa 401 döndür
                }

                return Ok(user);
            }
        }
    }

}


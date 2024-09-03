using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Core;
using Dapper;
using fazz.Models.Entities;
using fazz.Models.Requests;
using fazz.Models.Responses;
using fazz.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace fazz.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ClinicsController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ClinicsController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        public IActionResult Add(AddOrUpdateClinicsRequest request)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM clinics WHERE title = @Title and isActive=1";
                var existingQuestion = connection.QueryFirstOrDefault<Clinic>(
                    query,
                    new { Title = request.Title }
                );

                if (existingQuestion != null)
                {
                    return Conflict(new { Message = "Clinic already in use" }); // Title zaten kullanılıyorsa 409 döndür
                }

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var username = RandomGenerator.GenerateRandomUsername();
                        var password = "1111";
                        var user = new
                        {
                            name = request.Title,
                            surname = "",
                            email = request.Email,
                            password = password,
                            role = "clinic",
                            phoneNumber = "", //TODO
                            username = username                            
                        };

                        int userId = connection.QuerySingle<int>(
                            @"INSERT INTO users (name,surname,email,password,role,phoneNumber,username,isFirstPassword)
                            VALUES (@name, @surname, @email, @password,@role,@phoneNumber,@username,1);
                            SELECT LAST_INSERT_ID();",
                            user,
                            transaction
                        );

                        var clinic = new
                        {
                            title = request.Title,
                            description = request.Description,
                            address = request.Address,
                            webAddress = request.WebAddress,
                            email = request.Email,
                            userId = userId
                        };

                        int clinicId = connection.QuerySingle<int>(
                            @"
                    INSERT INTO clinics (title, description, address, webaddress,isActive,credit,email,userId)
                    VALUES (@title, @description, @address, @webAddress,1,0,@email,@userId );

                    SELECT LAST_INSERT_ID();",
                            clinic,
                            transaction
                        );

                        foreach (var category in request.Categories)
                        {
                            connection.Execute(
                                @"
                        INSERT INTO clinic_categories ( clinic_id, category_id)
                        VALUES (@ClinicId, @CategoryId);",
                                new { ClinicId = clinicId, CategoryId = category },
                                transaction
                            );
                        }

                        var creditUpdate = "INSERT INTO clinic_credits (clinicId, credit, add_credit, date_added) VALUES (@ClinicId, 0, 0, @DateAdded)";
                        connection.Execute(creditUpdate, new { ClinicId = clinicId, DateAdded = DateTime.Now.ToString("yyyy-MM-dd") }, transaction);



                        transaction.Commit();
                        return Ok(new { username, password });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        transaction.Rollback();
                        return StatusCode(500, new { Message = " could not be created" }); // Category oluşturulamazsa 500 döndür
                    }
                }
            }
        }

        [HttpGet]
        public IActionResult Get()
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM clinics where isActive = 1";
                var clinics = connection.Query<Clinic>(query);
                return Ok(clinics);
            }
        }

        [HttpGet]
        public IActionResult GetById(int id)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM clinics WHERE Id=@Id ";
                var clinic = connection.QueryFirstOrDefault<Clinic>(query, new { Id = id });

                if (clinic == null)
                {
                    return NotFound();
                }

                return Ok(clinic);
            }
        }

        [HttpGet]
        public IActionResult GetCredit(int clinicId)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT credit FROM clinics WHERE Id=@Id ";
                var credit = connection.QueryFirstOrDefault<int>(query, new { Id = clinicId });
                return Ok(credit);
            }
        }

        [HttpPost]
        public IActionResult Update(AddOrUpdateClinicsRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            var clinicUpdate = new Clinic
            {
                Id = request.Id,
                Title = request.Title,
                Address = request.Address,
                WebAddress = request.WebAddress,
                Description = request.Description
            };

            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query =
                    "UPDATE clinics SET Title = @Title,Address=@Address,WebAddress=@WebAddress,Description=@Description WHERE id = @Id";
                var result = connection.Execute(query, clinicUpdate);

                if (result > 0)
                {
                    return Ok();
                }
                else
                {
                    return NotFound();
                }
            }
        }

        [HttpDelete]
        public IActionResult Delete(int id)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var deleteClinicsQuery = "update clinics set isActive = 0 WHERE id = @id";
                var result = connection.Execute(deleteClinicsQuery, new { id });
                if (result > 0)
                {
                    return Ok();
                }
                else
                {
                    return NotFound();
                }
            }
        }

        [HttpPut]
        public IActionResult AddCreditToClinic(int clinicId, int clinicCredit, int newCredit)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var getCurrentCreditQuery = "SELECT credit FROM clinics WHERE id = @clinicId";
                        int currentCredit = connection.QuerySingle<int>(getCurrentCreditQuery, new { clinicId = clinicId }, transaction);

                        var addClinicCredit = "INSERT INTO clinic_credits (clinicId, credit, add_credit, date_added,previous_credit) VALUES (@clinicId, @credit, @add_credit, @date_added,@previous_credit)";
                        connection.Execute(
                            addClinicCredit,
                            new { clinicId = clinicId, credit = clinicCredit, add_credit = newCredit, date_added = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), previous_credit = currentCredit },
                            transaction: transaction);

                        var updateClinicsQuery = "UPDATE clinics SET credit = @credit WHERE id = @id";
                        var result = connection.Execute(
                            updateClinicsQuery,
                            new { credit = clinicCredit, id = clinicId },
                            transaction: transaction);

                        transaction.Commit();

                        if (result > 0)
                        {
                            return Ok();
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        // Hata günlüğü kaydı yapılabilir veya başka hata işlemleri yapılabilir
                        return StatusCode(500, "An error occurred: " + ex.Message);
                    }
                }
            }
        }

        [HttpGet]
        public IActionResult GetCreditHistoryss(int clinicId)
        {
           string connectionString = _config.GetConnectionString("schoolPortal");

             using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var getHistory = "SELECT * FROM clinic_credits WHERE clinicId = @clinicId";

                var result = connection.Query(getHistory, new { clinicId = clinicId }).ToList();
                
                if (result != null && result.Count > 0)
                {
                    return Ok(result);
                }
                else
                {
                    return NotFound();
                }
            }
        }



        [HttpPut]
        public IActionResult UpdateCategory([FromBody] List<int> categoryIds, int clinicId)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var deleteCategoiresQuery =
                            "delete from fazz.clinic_categories where clinic_id = @clinicId";
                        connection.Execute(deleteCategoiresQuery, new { clinicId }, transaction);

                        foreach (var item in categoryIds)
                        {
                            connection.Execute(
                                @"
                        INSERT INTO clinic_categories ( clinic_id, category_id)
                        VALUES (@ClinicId, @CategoryId);",
                                new { ClinicId = clinicId, CategoryId = item },
                                transaction
                            );
                        }
                        transaction.Commit();
                        return Ok();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, ex.Message);
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Dapper;
using fazz.Models.Entities;
using fazz.Models.Requests;
using fazz.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
namespace fazz.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class QuestionController : ControllerBase
    {
        private readonly IConfiguration _config;

        public QuestionController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        public IActionResult Add(AddOrUpdateQuestionRequest request)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM questions WHERE title = @Title";
                var existingQuestion = connection.QueryFirstOrDefault<Question>(query, new { Title = request.Title });

                if (existingQuestion != null)
                {
                    return Conflict(new { Message = "Question already in use" }); // Title zaten kullanılıyorsa 409 döndür
                }

                var question = new Question
                {
                    Title = request.Title,
                    CategoryId = request.CategoryId
                };

                var query2 = "INSERT INTO questions (title,category_id) VALUES (@Title, @CategoryId)";
                var result = connection.Execute(query2, question);
                var isCreated = result > 0;

                if (!isCreated)
                {
                    return StatusCode(500, new { Message = " could not be created" }); // Category oluşturulamazsa 500 döndür
                }

                return Ok(new { Message = "Question created successfully" });
            }
        }

        [HttpGet]
        public IActionResult Get()
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM questions";
                var questions = connection.Query<Question>(query);
                return Ok(questions);
            }
        }

        [HttpGet]
        public IActionResult GetById(int id)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM questions WHERE Id=@Id ";
                var question = connection.QueryFirstOrDefault<Question>(query, new { Id = id });

                if (question == null)
                {
                    return NotFound();
                }
                var response = new GetQuestionByIdResponse() { Question = question, IsSuccessful = true };


                return Ok(response);
            }
        }


        [HttpGet]
        public IActionResult GetByCategoryId(int categoryId)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "select * from fazz.questions where categoryId = @categoryId and isActive=true";

                var questions = connection.Query<Question>(query, new { categoryId = categoryId });

                if (questions == null || questions.Count() == 0)
                {
                    return NotFound();
                }
                


                return Ok(questions);
            }
        }


        [HttpPost]
        public IActionResult Update(AddOrUpdateQuestionRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            var questionUpdate = new Question
            {
                Title = request.Title,
                Id = request.Id,
            };

            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "UPDATE questions SET Title = @Title WHERE id = @Id";
                var result = connection.Execute(query, questionUpdate);

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

                var deleteQuestionsQuery = "DELETE FROM questions WHERE id = @id";
                var result = connection.Execute(deleteQuestionsQuery, new { id = id });
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


    }
}

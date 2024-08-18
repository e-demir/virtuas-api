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
    public class CategoryController : ControllerBase
    {
        private readonly IConfiguration _config;

        public CategoryController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        public IActionResult Add(Category request)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM categories WHERE title = @Title";
                var existingCategory = connection.QueryFirstOrDefault<Category>(
                    query,
                    new { Title = request.Title }
                );

                if (existingCategory != null)
                {
                    return Conflict(new { Message = "Category already in use" }); // Title zaten kullanılıyorsa 409 döndür
                }

                var category = new Category
                {
                    Title = request.Title,
                    Description = request.Description,
                    Credit = request.Credit,
                    IsActive = true
                };
                using var transaction = connection.BeginTransaction();
                try
                {
                    var category_id = connection.QuerySingle<int>(
                        @"INSERT INTO categories (title, description, credit,isActive) 
                        VALUES (@Title, @Description, @Credit, @IsActive);                
                        SELECT LAST_INSERT_ID();",
                        category,
                        transaction
                    );
                    if (request.Questions != null && request.Questions.Count > 0)
                    {
                        foreach (var item in request.Questions)
                        {
                            connection.Execute(
                                @"
                        INSERT INTO questions (title, categoryId, isActive)
                        VALUES (@_title, @_categoryId, @isActive);",
                                new { _title = item.Title, _categoryId = category_id, isActive = true },
                                transaction
                            );
                        }
                    }
                    transaction.Commit();
                    return Ok(new { Message = "Category created successfully" });
                }
                catch (System.Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, new { Message = " could not be created" }); // Category oluşturulamazsa 500 döndür
                }
            }
        }

        [HttpGet]
        public IActionResult Get()
        {
            string connectionString = _config.GetConnectionString("schoolPortal");
            var response = new List<Category>();
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var queryForCategories = "SELECT * FROM categories where isActive = 1";
                var categories = connection.Query<Category>(queryForCategories);
                foreach (var category in categories)
                {
                   var queryForQuestions = "SELECT * FROM questions q where "+
                   "categoryId=@catId and q.isActive = 1";
                   var questions = connection.Query<Question>(queryForQuestions, new { catId = category.Id });
                   category.Questions = questions.ToList();
                   response.Add(category);
                }
                return Ok(response);
            }
        }

        [HttpGet]
        public IActionResult GetById(int id)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");
            var response = new Category();
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "SELECT * FROM categories WHERE Id=@Id  ";
                var category = connection.QueryFirstOrDefault<Category>(query, new { Id = id });                 
                if (category == null)
                {
                    return NotFound();
                }
                else
                {
                    response = category;
                    var queryForQuestions = "SELECT * FROM questions q where "+
                   "categoryId=@catId";
                    var questions = connection.Query<Question>(queryForQuestions, new { catId = category?.Id }).ToList();
                    response.Questions = questions;
                }            
                return Ok(response);
            }
        }

        [HttpPost]
        public IActionResult Update(AddOrUpdateCategoryRequest request)
{
    if (request == null)
    {
        return BadRequest();
    }

    var categoryToUpdate = new Category
    {
        Title = request.Title,
        Description = request.Description,
        Id = request.Id,
        Credit = request.Credit,
        Questions = request.Questions
    };

    string connectionString = _config.GetConnectionString("schoolPortal");

    using (var connection = new MySqlConnection(connectionString))
    {
        connection.Open();
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                var query =
                    "UPDATE categories SET Title = @Title, Description = @Description, Credit = @Credit WHERE Id = @Id";
                var result = connection.Execute(query, categoryToUpdate, transaction);

                var query2 = "UPDATE questions SET isActive = 0 WHERE categoryId = @categoryId";
                connection.Execute(query2, new { categoryId = request.Id }, transaction);

                foreach (var item in request.Questions)
                {
                    var queryQuestion = "INSERT INTO questions (title, categoryId) VALUES (@title, @categoryId)";
                    connection.Execute(queryQuestion, new { title = item.Title, categoryId = item.CategoryId }, transaction);
                }

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
                // Log the exception message
                Console.WriteLine($"An error occurred: {ex.Message}");
                return StatusCode(500, "An error occurred while updating the category.");
            }
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

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var deleteQuestionsQuery =                            
                             "UPDATE questions SET isActive = 0 WHERE categoryId = @CategoryId";
                        connection.Execute(
                            deleteQuestionsQuery,
                            new { CategoryId = id },
                            transaction
                        );

                        var deleteCategoryQuery = "UPDATE categories SET isActive = 0 WHERE id = @Id";
                        var result = connection.Execute(
                            deleteCategoryQuery,
                            new { Id = id },
                            transaction
                        );

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
                    catch
                    {
                        transaction.Rollback();
                        return StatusCode(
                            500,
                            "An error occurred while deleting the category and its questions."
                        );
                    }
                }
            }
        }

        [HttpPost]
        public IActionResult GetByClinicId(int clinicId)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query =
                    "select ca.* from fazz.clinics c "
                    + "join fazz.clinic_categories cm on c.id = cm.clinic_id "
                    + "join fazz.categories ca on ca.id = cm.category_id "
                    + "where c.id = @id";
                var categories = connection.Query<Category>(query, new { id = clinicId });
                return Ok(categories);
            }
        }
    }
}

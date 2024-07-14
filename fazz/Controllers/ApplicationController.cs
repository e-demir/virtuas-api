﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Dapper;
using fazz.Models.Entities;
using fazz.Models.Requests;
using fazz.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using static System.Net.Mime.MediaTypeNames;
using Application = fazz.Models.Entities.Application;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace fazz.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ApplicationController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ApplicationController(IConfiguration config)
        {
            _config = config;
        }



        [HttpPost]
        public IActionResult Add(int userId, int categoryId, [FromBody] List<Answer> answers)
        {
            DateTime date = DateTime.Now;

            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var query = "INSERT INTO applications (userId, categoryId, date) VALUES (@UserId, @CategoryId, @Date)";
                        connection.Execute(query, new { UserId = userId, CategoryId = categoryId, Date = date }, transaction);

                        // Son eklenen applicationId'yi almak için
                        var applicationId = connection.QuerySingle<int>("SELECT LAST_INSERT_ID()", transaction: transaction);

                        foreach (var answer in answers)
                        {
                            var answerQuery = "INSERT INTO answers (title , question_id, application_id) VALUES (@Title, @QuestionId ,@ApplicationId)";
                            connection.Execute(answerQuery, new { Title = answer.Title, QuestionId = answer.QuestionId, ApplicationId = applicationId  }, transaction);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, "An error occurred while processing your request.");
                    }
                }
            }

            return Ok();
        }

        [HttpGet]
        public IActionResult GetApplicationsWithAnswers(int userId)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            var res = new ApplicationDetailsResponse() {  ApplicationDetails = new List<Application>() };

            using (var connection = new MySqlConnection(connectionString))
            {
                var applications = new List<Application>();
                
                var answers = new List<AnswerAndQuestion>();
                connection.Open();

                var query = "SELECT  a.id as ApplicationId, a.date as ApplicationDate, ca.title as CategoryTitle, ca.description as CategoryDescription   " +
                    "FROM applications a LEFT JOIN categories ca ON ca.Id = a.categoryId where a.userId = @userId";
                var response= connection.Query<Application>(query, new { userid = userId});

                applications = response.ToList();

                foreach (var item in applications)
                {
                    var appModel = new Application();
                    appModel.ApplicationDate = item.ApplicationDate;
                    appModel.ApplicationId = item.ApplicationId;
                    appModel.CategoryDescription = item.CategoryDescription;
                    appModel.CategoryTitle = item.CategoryTitle;

                    var query2 = "select ans.title as AnswerTitle, q.title as QuestionTitle from applications a " +
                    "left join answers ans on ans.application_id = a.id left join questions q on q.id = ans.question_id where a.id = @applicationId";

                    var response2 = connection.Query<AnswerAndQuestion>(query2, new { applicationId = item.ApplicationId });

                    var answersFetched = response2.ToList();

                    appModel.Answers = answersFetched;

                    res.ApplicationDetails.Add(appModel);
                }

                return Ok(res);
            }
        }


        [HttpGet]
        public IActionResult GetApplicationsPreData(int clinicId)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            var res = new PossibleClientPreDataResponse();

            using (var connection = new MySqlConnection(connectionString))
            {
                var applications = new List<PossibleClientPreData>();


                connection.Open();

                var query = "select app.id as applicationId,app.userId as userId,ca.title as categoryTitle,u.name as userName,u.surname as userSurname from applications app join users u on u.id=app.userId join categories ca on ca.id=app.categoryId where app.categoryId in (select cl_ca.category_id from clinic_categories cl_ca where cl_ca.clinic_id=@clinicId)";
                var response = connection.Query<PossibleClientPreData>(query, new { clinicId = clinicId});

                applications = response.ToList();

                foreach (var item in applications)
                {
                    var answers = new List<AnswerAndQuestion>();



                    var query2 = "select ans.title as AnswerTitle ,q.title as QuestionTitle from answers ans join questions q on ans.question_id=q.id where ans.application_id=@app_id";

                    var response2 = connection.Query<AnswerAndQuestion>(query2, new { app_id = item.ApplicationId });

                    var answersFetched = response2.ToList();

                    item.Answers = answersFetched;

                    res.PreDataList.Add(item);
                 
                }

                return Ok(res);
            }
        }

    }
}


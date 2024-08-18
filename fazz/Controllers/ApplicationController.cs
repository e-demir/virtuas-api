using System;
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

                    var query3 = "select count(*) from offers where applicationId = @applicationId";
                    var response3 = connection.QuerySingle<int>(query3, new { applicationId = item.ApplicationId });

                    appModel.OfferCount = response3;

                    var queery4 = "select c.title from fazz.offers o left join fazz.clinics c on c.id = o.clinicId where o.applicationId = @applicationId";
                    var response4 = connection.Query<string>(queery4, new { applicationId = item.ApplicationId });

                    appModel.OfferedClinics = (List<string>)response4;

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
                 var query = "SELECT app.id AS applicationId,    app.userId AS userId,    ca.title AS categoryTitle,    ca.credit AS cost,    u.name AS userName,    u.surname AS userSurname FROM     applications app     JOIN users u ON u.id = app.userId     JOIN categories ca ON ca.id = app.categoryId     LEFT JOIN offers o ON o.applicationId = app.id AND o.clinicId = @clinicId WHERE     app.categoryId IN (        SELECT cl_ca.category_id        FROM clinic_categories cl_ca         WHERE cl_ca.clinic_id = @clinicId    )    AND o.applicationId IS NULL;";

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


         [HttpGet]
        public IActionResult GetAllApplicationsWithAnswers()
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                var applications = new List<DetailApplication>();
                
                var answers = new List<AnswerAndQuestion>();
                connection.Open();

                var query = "SELECT  u.name as Username,u.surname as UserSurname,a.id as ApplicationId, a.date as ApplicationDate, ca.title as CategoryTitle, ca.description as CategoryDescription FROM applications a LEFT JOIN categories ca ON ca.Id = a.categoryId LEFT JOIN users u ON u.Id=a.userId ";
                var response= connection.Query<DetailApplication>(query);

                applications = response.ToList();

                foreach (var item in applications)
                {
                   
                    var query2 = "select ans.title as AnswerTitle, q.title as QuestionTitle from applications a " +
                    "left join answers ans on ans.application_id = a.id left join questions q on q.id = ans.question_id where a.id = @applicationId";

                    var response2 = connection.Query<AnswerAndQuestion>(query2, new { applicationId = item.ApplicationId });

                    var answersFetched = response2.ToList();

                    item.Answers = answersFetched;

                }

                return Ok(applications);
            }
        }

    }
}



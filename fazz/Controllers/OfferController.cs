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
    public class OfferController : ControllerBase
    {
        private readonly IConfiguration _config;

        public OfferController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        public IActionResult Make(OfferRequest request)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // offer ekle
                        var insertOffer =
                            "INSERT INTO offers (clinicId,applicationId, price, offerDate)VALUES(@clinicId,@applicationId,@price,@offerDate);";
                        connection.Execute(
                            insertOffer,
                            new
                            {
                                clinicId = request.ClinicId,
                                applicationId = request.ApplicationId,
                                price = request.Price,
                                offerDate = DateTime.Now,
                            },
                            transaction
                        );

                     
                         var getCurrentCreditQuery = "SELECT credit FROM clinics WHERE id = @clinicId";
                        int currentCredit = connection.QuerySingle<int>(getCurrentCreditQuery, new { clinicId = request.ClinicId }, transaction);

                        


                           // price düşür kategoriden
                        var updateCredit =
                            "UPDATE clinics SET credit = credit - @price WHERE id = @clinicId;";
                        connection.Execute(
                            updateCredit,
                            new { clinicId = request.ClinicId, price = request.Price },
                            transaction
                        );
                       
    
                        var addClinicCredit = "INSERT INTO clinic_credits (clinicId,credit, add_credit, date_added,previous_credit) VALUES (@clinicId, (SELECT credit FROM clinics WHERE id = @clinicId), @add_credit, @date_added,@previous_credit)";
                        connection.Execute(
                            addClinicCredit,
                            new { clinicId = request.ClinicId, add_credit = -Math.Abs(request.Price), date_added = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),previous_credit=currentCredit},
                            transaction
                            );

                        transaction.Commit();
                        return Ok();
                    }
                    catch (System.Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, $"{ex.Message}");
                    }
                }
            }
        }

        [HttpGet]
        public IActionResult GetMade(int? clinicId)
        {
            string connectionString = _config.GetConnectionString("schoolPortal");

            var res = new ClientDataResponse();

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var clientData = new List<ClientData>();

                try
                {
                    var query =
                        "SELECT     app.id AS applicationId,    ca.title AS categoryTitle,    ca.credit AS cost,    u.name AS userName,    u.surname AS userSurname,    u.email AS userEmail,    u.phoneNumber as userPhone,    o.offerDate as offerDate FROM     applications app     JOIN users u ON u.id = app.userId     JOIN categories ca ON ca.id = app.categoryId        LEFT JOIN offers o ON o.applicationId = app.id AND o.clinicId = @clinicId  WHERE     app.categoryId IN (        SELECT cl_ca.category_id         FROM clinic_categories cl_ca         WHERE cl_ca.clinic_id = @clinicId    )    AND o.applicationId IS not NULL;";

                    var response = connection.Query<ClientData>(query, new { clinicId });

                    clientData = response.ToList();

                    foreach (var item in clientData)
                    {
                        var answers = new List<AnswerAndQuestion>();

                        var query2 =
                            "select ans.title as AnswerTitle ,q.title as QuestionTitle from answers ans join questions q on ans.question_id=q.id where ans.application_id=@app_id";

                        var response2 = connection.Query<AnswerAndQuestion>(
                            query2,
                            new { app_id = item.ApplicationId }
                        );

                        var answersFetched = response2.ToList();

                        item.Answers = answersFetched;

                        res.ClientDataList.Add(item);
                    }

                    return Ok(res);
                }
                catch (System.Exception ex)
                {
                    return StatusCode(500, $"{ex.Message}");
                }
            }
        }
    }
}

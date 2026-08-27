using System;
using System.Linq;
using System.Net;
using System.Web.Http;
using ESTAFF.Models.Data;
using ESTAFF.Services;


namespace ESTAFF.Controllers.Api
{
    [RoutePrefix("api/tasks")]
    public class TaskApiController : ApiController
    {
        private readonly TaskService _taskService;
        private readonly ApplicationDbContext _db;

        public TaskApiController()
        {
            _db = new ApplicationDbContext();
            _taskService = new TaskService(_db);
        }

        // GET
        [HttpGet]
        [Route("{plantId}")]
        public IHttpActionResult Get(int plantId)
        {
            var result = _taskService.GetCOF(plantId)
            .Select(c => new {
                    c.Id,
                    c.PlantId,
                    c.RegistrationNo,
                    c.ExpiryDate,
                    c.MachineName,
                    c.Status,
                    c.Location,
                    c.Department,
                });
            return Ok(result);
        }
    }
}
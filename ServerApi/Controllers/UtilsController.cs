using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ServerApi.Options;

namespace ServerApi.Controllers
{
    [Route("api/[controller]/[action]")]
    public class UtilsController : Controller
    {
        private ApiOptions _apiOptions;
        
        public UtilsController(IOptions<ApiOptions> options)
        {
            _apiOptions = options.Value;
        }

        // GET api/utils/ping
        [HttpGet]
        public JsonResult Ping()
        {
            return Json(DateTime.Now.ToString());
        }

        [HttpGet]
        public string Time()
        {
            return DateTime.Now.ToString();
        }
    }
}

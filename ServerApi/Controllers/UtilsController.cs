using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ServerApi.Options;
using Utils;

namespace ServerApi.Controllers
{
    public class UtilsController : Controller
    {
        private ApiOptions _apiOptions;
        
        public UtilsController(IOptions<ApiOptions> options)
        {
            _apiOptions = options.Value;
        }

        [HttpGet(WebApi.Ping)]
        public JsonResult Ping()
        {
            return Json(DateTime.Now.ToString());
        }
    }
}

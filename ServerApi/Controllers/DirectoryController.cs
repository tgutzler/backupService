using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ServerApi.Database;
using ServerApi.Interfaces;
using ServerApi.Options;
using Utils;

namespace ServerApi.Controllers
{
    public class DirectoryController : Controller
    {
        private ApiOptions _apiOptions;
        private DatabaseService _dbService;

        public DirectoryController(IOptions<ApiOptions> options)
        {
            _apiOptions = options.Value;
            _dbService = DatabaseService.Instance;
        }

        [HttpGet(WebApi.GetDirectory)]
        public string Get()
        {
            var count = _dbService.DirectoryCount;
            if (count == 1)
                return $"There is {_dbService.DirectoryCount} directory backed up";
            else
                return $"There are {_dbService.DirectoryCount} directories backed up";
        }

        [HttpGet(WebApi.GetDirectory + "/{id}")]
        public IActionResult Get(int id)
        {
            try
            {
                return Json(_dbService.GetDirectory(id));
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        // POST api/directory
        [HttpPost(WebApi.GetDirectory)]
        [HttpPost(WebApi.GetDirectoryWithFiles)]
        public IActionResult Get([FromBody] BUDirectoryInfo di)
        {
            var includeChildren = Url.Action().EndsWith(WebApi.GetDirectoryWithFiles);
            try
            {
                return di.ParentId == null ?
                    Json(_dbService.GetDirectory(di.Path, includeChildren)) :
                    Json(_dbService.GetDirectory(di.Name, (int)di.ParentId, includeChildren: includeChildren));
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        // POST api/directory/add
        [HttpPost(WebApi.AddDirectory)]
        public IActionResult Add([FromBody] BackedUpDirectory directory)
        {
            if (directory == null)
            {
                return BadRequest();
            }

            try
            {
                _dbService.AddDirectory(directory);
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
            return Json(directory);
        }

        [HttpPost(WebApi.UpdateDirectory)]
        public IActionResult Update([FromBody] BackedUpDirectory directory)
        {
            if (directory == null)
            {
                return BadRequest();
            }

            try
            {
                _dbService.UpdateDirectory(directory);
                return Json(directory);
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }
    }
}

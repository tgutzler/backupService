using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ServerApi.Database;
using ServerApi.Options;

namespace ServerApi.Controllers
{
    [Route("api/[controller]")]
    public class DirectoryController : Controller
    {
        private ApiOptions _apiOptions;
        private DatabaseService _dbService;

        public DirectoryController(IOptions<ApiOptions> options)
        {
            _apiOptions = options.Value;
            _dbService = DatabaseService.Instance;
        }

        // GET api/directory
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/directory/s8f4
        [HttpGet("{hash}")]
        public BackedUpDirectory Get(string hash)
        {
            return _dbService.GetDirectory(hash);
        }

        // POST api/directory
        [HttpPost]
        public IActionResult Post([FromBody] BackedUpDirectory directory)
        {
            if (directory == null)
            {
                return BadRequest();
            }

            _dbService.AddDirectory(directory);
            return CreatedAtRoute("api/directory", new { id = directory.Id }, directory);
        }

        [HttpPut("{hash}")]
        public IActionResult Update(string hash, [FromBody] BackedUpDirectory directory)
        {
            if (directory == null)// || MD5(directory.Path) != hash)
            {
                return BadRequest();
            }

            if (_dbService.UpdateDirectory(directory))
            {
                return new NoContentResult();
            }
            
            return NotFound();
        }
    }
}

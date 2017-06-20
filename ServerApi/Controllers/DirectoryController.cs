﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ServerApi.Database;
using ServerApi.Interfaces;
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
        public string Get()
        {
            var count = _dbService.DirectoryCount;
            if (count == 1)
                return $"There is {_dbService.DirectoryCount} directory backed up";
            else
                return $"There are {_dbService.DirectoryCount} directories backed up";
        }

        // Get api/directory/1
        [HttpGet("{id}")]
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

        // GET api/directory
        [HttpPost]
        public IActionResult Get([FromBody] BUDirectoryInfo di)
        {
            try
            {
                return di.ParentId == null ?
                    Json(_dbService.GetDirectory(di.Path)) :
                    Json(_dbService.GetDirectory(di.Name, (int)di.ParentId));
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        // GET api/directory/sdf?parentId=1
        //[HttpGet("{name}")]
        //public IActionResult Get(string name, int parentId)
        //{
        //    try
        //    {
        //        return Json(_dbService.GetDirectory(name, parentId));
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(ex);
        //    }
        //}

        // POST api/directory
        [HttpPost("Add")]
        public IActionResult Post([FromBody] BackedUpDirectory directory)
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

        [HttpPut("Update")]
        public IActionResult Put([FromBody] BackedUpDirectory directory)
        {
            if (directory == null)
            {
                return BadRequest();
            }

            try
            {
                if (_dbService.UpdateDirectory(directory))
                {
                    return Json(directory);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
            
            return NotFound();
        }
    }
}

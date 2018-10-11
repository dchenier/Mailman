﻿using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using EnsureThat;
using Mailman.Server.Models;
using Mailman.Services.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Mailman.Controllers
{
    /// <summary>
    /// Controller for Merge Templates
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MergeTemplatesController : ControllerBase
    {
        private readonly IMergeTemplateRepository _mergeTemplateRepository;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        /// <summary>
        /// Constructor for merge templates
        /// </summary>
        /// <param name="mergeTemplateRepository">Service to merge template persistance store</param>
        /// <param name="mapper">Automapper instance</param>
        public MergeTemplatesController(
            IMergeTemplateRepository mergeTemplateRepository,
            IMapper mapper,
            ILogger logger)
        {
            EnsureArg.IsNotNull(mergeTemplateRepository, nameof(mergeTemplateRepository));
            EnsureArg.IsNotNull(mapper, nameof(mapper));
            EnsureArg.IsNotNull(logger, nameof(logger));
            _mergeTemplateRepository = mergeTemplateRepository;
            _mapper = mapper;
            _logger = logger;
        }

        // GET: api/MergeTemplate
        /// <summary>
        /// Retrieves all the merge templates for a given spreadsheet
        /// </summary>
        /// <param name="spreadsheetId">The id of the spreadsheet, as in the url when editing a sheet</param>
        /// <returns>A list of merge templates for the given spreadsheet</returns>
        [HttpGet("{spreadsheetId}")]
        public async Task<IActionResult>  Get(string spreadsheetId)
        {
            IEnumerable<Services.Data.MergeTemplate> mergeTemplates;
            try { mergeTemplates = await _mergeTemplateRepository.GetMergeTemplatesAsync(spreadsheetId); }
            catch (SheetNotFoundException snfe)
            {
                _logger.Warning("Spreadsheet '{SpreadSheetId} not foud", spreadsheetId);
                return NotFound();
            }
            return Ok(_mapper.Map<IEnumerable<Services.Data.MergeTemplate>, IEnumerable<MergeTemplate>>(mergeTemplates));
        }
       

        //// POST: api/MergeTemplate
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT: api/MergeTemplate/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE: api/ApiWithActions/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}

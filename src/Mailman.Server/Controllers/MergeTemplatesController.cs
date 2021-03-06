﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using EnsureThat;
using Mailman.Server.Hubs;
using Mailman.Server.Models;
using Mailman.Services;
using Mailman.Services.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Mailman.Server.Controllers
{
    /// <summary>
    /// Controller for Merge Templates.
    /// </summary>
    [Authorize(AuthenticationSchemes = AuthSchemes)]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ApiController]

    public class MergeTemplatesController : ControllerBase
    {
        private const string AuthSchemes =
            CookieAuthenticationDefaults.AuthenticationScheme + "," +
            JwtBearerDefaults.AuthenticationScheme;
        private readonly IMergeTemplateRepository _mergeTemplateRepository;
        private readonly IHubContext<MailmanHub> _mailmanHub;
        private readonly IMailmanServicesProxy _servicesProxy;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        /// <summary>
        /// Constructor for merge templates
        /// </summary>
        /// <param name="mergeTemplateRepository">Service to merge template persistance store.</param>
        /// <param name="mailmanHub"></param>
        /// <param name="servicesProxy"></param>
        /// <param name="mapper">Automapper instance</param>
        /// <param name="logger">Serilog logger</param>
        public MergeTemplatesController(
            IMergeTemplateRepository mergeTemplateRepository,
            IHubContext<MailmanHub> mailmanHub,
            IMailmanServicesProxy servicesProxy,
            IMapper mapper,
            ILogger logger)
        {
            EnsureArg.IsNotNull(mergeTemplateRepository, nameof(mergeTemplateRepository));
            EnsureArg.IsNotNull(mailmanHub, nameof(mailmanHub));
            EnsureArg.IsNotNull(servicesProxy, nameof(servicesProxy));
            EnsureArg.IsNotNull(mapper, nameof(mapper));
            EnsureArg.IsNotNull(logger, nameof(logger));
            _mergeTemplateRepository = mergeTemplateRepository;
            _mailmanHub = mailmanHub;
            _servicesProxy = servicesProxy;
            _mapper = mapper;
            _logger = logger;
        }

        // GET: api/MergeTemplate
        /// <summary>
        /// Retrieves all the merge templates for a given spreadsheet.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///     GET /api/MergeTemplates/1GnoG6twy6OC9jQw7-KeZBR02znTW8VkR7Yp2Wf2JlrY
        /// </remarks>
        /// <param name="spreadsheetId">The id of the spreadsheet, as in the url when editing a sheet.</param>
        /// <returns>A list of merge templates for the given spreadsheet.</returns>
        /// <response code="200">Returns the merge templates for the Sheet.</response>
        /// <response code="404">If the Google Sheet cannot be found.</response>
        [HttpGet("{spreadsheetId}")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<MergeTemplate>))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Get(string spreadsheetId)
        {
            IEnumerable<Services.Data.MergeTemplate> mergeTemplates;
            try { mergeTemplates = await _mergeTemplateRepository.GetMergeTemplatesAsync(spreadsheetId); }
            catch (SheetNotFoundException)
            {
                _logger.Warning("Spreadsheet '{SpreadSheetId} not foud", spreadsheetId);
                return NotFound();
            }
            return Ok(_mapper.Map<IEnumerable<Services.Data.MergeTemplate>, IEnumerable<MergeTemplate>>(mergeTemplates));
        }

        // POST: api/MergeTemplates/Email
        /// <summary>
        /// Create a new merge template and save it to the database
        /// </summary>
        /// <returns></returns>
        [HttpPost("Email")]
        public async Task<IActionResult> PostMergeTemplate([FromBody] EmailMergeTemplate mergeTemplateData)
        {
            EnsureArg.IsNotNull(mergeTemplateData);

            if (!ModelState.IsValid)
            {
                _logger.Warning("Unable to create MergeTemplate because model state is not valid: {ModelState}", ModelState);
                return BadRequest(ModelState);
            }

            var newMergeTemplate = _mapper.Map<Mailman.Services.Data.MergeTemplate>(mergeTemplateData);
            try
            {
                await _mergeTemplateRepository.AddMergeTemplateAsync(newMergeTemplate);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Unable to save the data to datbase");
                throw e;
            }
            return CreatedAtAction("Created", newMergeTemplate);
        }


        // PUT: api/MergeTemplates/Email
        /// <summary>
        /// Updates a merge template with new values
        /// </summary>
        /// <returns></returns>
        [HttpPut("Email")]
        public async Task<IActionResult> Put([FromBody] EmailMergeTemplate mergeTemplateData)
        {
            EnsureArg.IsNotNull(mergeTemplateData);

            if (!ModelState.IsValid)
            {
                _logger.Warning("Unable to update MergeTemplate because model state is not valid: {ModelState}", ModelState);
                return BadRequest(ModelState);
            }
            var newMergeTemplate = _mapper.Map<Mailman.Services.Data.MergeTemplate>(mergeTemplateData);
            try
            {
                await _mergeTemplateRepository.UpdateMergeTemplateAsync(newMergeTemplate);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Unable to save the data to datbase");
                throw e;
            }

            return Ok(newMergeTemplate);
        }

        // DELETE: api/ApiWithActions/5
        /// <summary>
        /// Deletes a merge template
        /// </summary>
        /// <returns></returns>
        [HttpDelete("Email")]
        public async Task<IActionResult> Delete([FromBody]RunMailMergeOptions options)

        {

            if (!ModelState.IsValid)
            {
                _logger.Warning("Unable to delete MergeTemplate because model state is not valid: {ModelState}", ModelState);
                return BadRequest(ModelState);
            }
            var oldMergeTemplate = await _mergeTemplateRepository.GetMergeTemplate(options.MergeTemplateId);
            try
            {
                await _mergeTemplateRepository.DeleteMergeTemplateAsync(oldMergeTemplate);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Unable to save the data to datbase");
                throw e;
            }

            return Ok(oldMergeTemplate);
        }

        /// <summary>
        /// Starts a new mail merge in a new worker process and returns immediately
        /// </summary>
        /// <param name="options">
        /// Parameters to start a mail merge, 
        /// including MergeTemplateId and optionally a SignalR connectionId 
        /// for proress notifications on the merge.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost("start")]
        public async Task<IActionResult> StartMailMerge(RunMailMergeOptions options,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var mergeTemplate = await _mergeTemplateRepository.GetMergeTemplate(options.MergeTemplateId);
            if (mergeTemplate == null)
                return NotFound($"Merge template with id {options.MergeTemplateId} not found");

            _servicesProxy.StartMailMerge(options);
            return Ok();
        }
    }
}

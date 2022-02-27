using System.Threading.Tasks;
using Drako.Api.Configuration;
using Drako.Api.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz.Impl;
using Quartz.Impl.Matchers;

namespace Drako.Api.Controllers.Jobs
{
    [Authorize(Roles = Roles.Owner)]
    public class JobsController : Controller
    {
        private readonly SyncWithTwitchJob _syncJob;
        private readonly AddCurrencyJob _addCurrencyJob;

        public JobsController(SyncWithTwitchJob syncJob, AddCurrencyJob addCurrencyJob)
        {
            _syncJob = syncJob;
            _addCurrencyJob = addCurrencyJob;
        }

        [HttpGet("admin/sync")]
        public async Task<IActionResult> Sync()
        {
            await _syncJob.Execute(null);
            return Ok();
        }

        [HttpPost("admin/addCurrency")]
        public async Task<IActionResult> AddCurrency([FromQuery] string groupingId = null)
        {
            var jobExecutionContext = new JobExecutionContext();
            jobExecutionContext.Put("groupingId", groupingId);
            await _addCurrencyJob.Execute(jobExecutionContext);
            return Ok();
        }
    }
}
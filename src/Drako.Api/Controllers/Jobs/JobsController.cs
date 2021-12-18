using System.Threading.Tasks;
using Drako.Api.Configuration;
using Drako.Api.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drako.Api.Controllers.Jobs
{
    [Authorize(Roles = Roles.Owner)]
    public class JobsController : Controller
    {
        private readonly SyncWithTwitchJob _syncJob;

        public JobsController(SyncWithTwitchJob syncJob)
        {
            _syncJob = syncJob;
        }

        [HttpGet("admin/sync")]
        public async Task<IActionResult> Sync()
        {
            await _syncJob.Execute(null);
            return Ok();
        }
    }
}
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.ParentGuard.Controllers
{
    [ApiController]
    [Route("ParentGuard/users")] 
    [Authorize(Policy = "RequiresElevation")] // admin only
    public class UsersController : ControllerBase
    {
        private readonly IUserManager? _users;
        public UsersController(IUserManager users)
        {
            _users = users;
        }

        // Fallback
        public UsersController()
        {
            _users = Services.ServiceHub.UserManager;
        }

        [HttpGet]
        public IActionResult Get()
        {
            if (_users == null) return Ok(new { items = new object[] { } });
            var list = _users.Users.Select(u => new { id = u.Id.ToString(), name = u.Username }).ToArray();
            return Ok(new { items = list });
        }
    }
}



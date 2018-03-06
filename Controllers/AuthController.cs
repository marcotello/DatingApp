using System.Threading.Tasks;
using DatingApp.API.Data;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace DatingApp.API.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private readonly IAuthRepository _repo;
        public AuthController(IAuthRepository repo)
        {
            _repo = repo;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(string username, string password)
        {
            // validate the request

            username = username.ToLower();

            if(await _repo.UserExists(username))
            {
                return BadRequest("Username is already taken");
            }

            var userToCreate = new User
            {
                Username = username
            };

            var createUSer = await _repo.Register(userToCreate, password);

            return StatusCode(201);
        }
    }
}
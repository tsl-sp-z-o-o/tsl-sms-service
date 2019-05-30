using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TslWebApp.Data;
using TslWebApp.Models;

namespace TslWebApp.Controllers
{
    public class UserController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public UserController(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Index(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ReturnMessage"] = $"No user of id {id}";
                TempData["AlertType"] = "danger";
                return RedirectToAction("Index", "Home");
            }

            var userModel = new UserModel()
            {
                Email = user.Email,
                Id = id,
                PhoneNumber = user.PhoneNumber
            };

            return View(userModel);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(UserModel userModel)
        {
            if (userModel != null) {
                var user = await _userManager.FindByEmailAsync(userModel.Email);
                if (user != null)
                {
                    var passwordSingInResult = await _signInManager.PasswordSignInAsync(user, userModel.Password, false, false);
                    if (passwordSingInResult.Succeeded)
                    {
                        return RedirectToAction(nameof(Index), new { id = user.Id });
                    }
                }
                TempData["ReturnMessage"] = "Couldn't log in.";
                TempData["AlertType"] = "danger";
            }
            return View(nameof(Login));
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["ReturnMessage"] = "Signed out successfully.";
            TempData["AlertType"] = "success";
            return RedirectToAction("Index","Home");
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> EditUser(string id)
        {
            var updateResult = await _userManager.UpdateAsync(await _userManager.FindByIdAsync(id));
 
            if (updateResult.Succeeded)
            {
                TempData["ReturnMessage"] = "Successfully updated.";
                TempData["AlertType"] = "success";
            }
            else
            {
                TempData["ReturnMessage"] = "Couldn't update information because of "+updateResult.Errors;
                TempData["AlertType"] = "danger";
            }
            return RedirectToAction(nameof(Index), id);
        }
    }
}
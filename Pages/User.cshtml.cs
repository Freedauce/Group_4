using Exam.Models;
using Exam.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Exam.Pages
{
    [Authorize]
    public class UserModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public UserModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public ApplicationUser appUser { get; set; }
        public string UserRole { get; set; }
        public UserStatsDto UserStats { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var username = User.Identity.Name;
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToPage("/Account/Login");
            }

            appUser = await _userManager.FindByNameAsync(username);
            if (appUser == null)
            {
                return RedirectToPage("/Account/Login");
            }

            // Get user role
            var roles = await _userManager.GetRolesAsync(appUser);
            UserRole = roles.FirstOrDefault() ?? "client";

            // Load statistics for clients
            if (UserRole == "client")
            {
                UserStats = new UserStatsDto
                {
                    TotalBookings = await _context.Bookings.CountAsync(b => b.ClientId == appUser.Id),
                    ActiveBookings = await _context.Bookings.CountAsync(b => b.ClientId == appUser.Id && b.Status == "Approved" && !b.FullPaymentPaid),
                    CompletedBookings = await _context.Bookings.CountAsync(b => b.ClientId == appUser.Id && b.FullPaymentPaid),
                    TotalSpent = await _context.Bookings
                        .Where(b => b.ClientId == appUser.Id && b.FullPaymentPaid)
                        .SumAsync(b => (decimal?)b.TotalAmount) ?? 0
                };
            }

            return Page();
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await _signInManager.SignOutAsync();
            return RedirectToPage("/Index");
        }
    }

    public class UserStatsDto
    {
        public int TotalBookings { get; set; }
        public int ActiveBookings { get; set; }
        public int CompletedBookings { get; set; }
        public decimal TotalSpent { get; set; }
    }
}
using Exam.Models;
using Exam.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Exam.Pages
{
    [Authorize(Roles = "admin")]
    public class AdminModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Dashboard metrics
        public int TotalUsers { get; set; }
        public int TotalManagers { get; set; }
        public int TotalClients { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }

        // System performance (simulated values)
        public double CpuUsage { get; set; } = 25.5;
        public double MemoryUsage { get; set; } = 62.3;
        public int ActiveSessions { get; set; } = 12;
        public string SystemUptime { get; set; } = "5d 12h 30m";

        // Data lists
        public List<UserDto> Users { get; set; } = new();
        public List<LogDto> RecentLogs { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            // Load user statistics
            var allUsers = await _userManager.Users.ToListAsync();
            TotalUsers = allUsers.Count;

            // Count managers and clients
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("manager")) TotalManagers++;
                if (roles.Contains("client")) TotalClients++;
            }

            // Load booking statistics
            TotalBookings = await _context.Bookings.CountAsync();
            TotalRevenue = await _context.Bookings
                .Where(b => b.Status == "Approved" && b.FullPaymentPaid)
                .SumAsync(b => b.TotalAmount);

            // Load users with roles
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "client";
                Users.Add(new UserDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Role = role,
                    CreatedAt = user.CreatedAt
                });
            }

            // Load recent logs with user names
            var logs = await _context.SystemLogs
                .OrderByDescending(l => l.CreatedAt)
                .Take(20)
                .ToListAsync();

            RecentLogs = new List<LogDto>();
            foreach (var log in logs)
            {
                var user = await _userManager.FindByIdAsync(log.UserId);
                RecentLogs.Add(new LogDto
                {
                    Action = log.Action,
                    Details = log.Details,
                    UserName = user?.UserName ?? "System",
                    IpAddress = log.IpAddress,
                    CreatedAt = log.CreatedAt
                });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAddUserAsync(string UserName, string Email, string Password, string Role)
        {
            var user = new ApplicationUser
            {
                UserName = UserName,
                Email = Email,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, Role);

                // Log activity
                await LogActivity("User Created", $"Created new user: {UserName} with role: {Role}");
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditUserAsync(string UserId, string UserName, string Email, string Role)
        {
            var user = await _userManager.FindByIdAsync(UserId);
            if (user != null)
            {
                user.UserName = UserName;
                user.Email = Email;

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    // Update role
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, Role);

                    await LogActivity("User Updated", $"Updated user: {UserName} with role: {Role}");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return Page();
                }
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var userName = user.UserName;

                // FIX 1: Delete dependent records in Booking and SystemLogs tables

                // --- 1.1 Delete associated SystemLogs ---
                var logsToDelete = await _context.SystemLogs.Where(l => l.UserId == userId).ToListAsync();
                _context.SystemLogs.RemoveRange(logsToDelete);

                // --- 1.2 Delete associated Bookings (New Fix) ---
                // NOTE: If Payment table references Booking, then deleting the Booking will automatically delete 
                // associated Payments *IF* cascade delete is configured in EF Core migrations.
                // Assuming Payment has a foreign key to Booking:

                var bookingsToDelete = await _context.Bookings
                                                     // If the ClientId field exists on Booking, use it:
                                                     .Where(b => b.ClientId == userId)
                                                     .ToListAsync();

                _context.Bookings.RemoveRange(bookingsToDelete);

                // Save changes to clear foreign key dependencies before deleting the user
                await _context.SaveChangesAsync();

                // 2. Delete the Identity user
                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    await LogActivity("User Deleted", $"Deleted user: {userName}");
                }
                else
                {
                    // Handle Identity errors
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    // Log the failure in case the deletion process left orphaned records
                    await LogActivity("User Deletion Failed", $"Failed to delete user: {userName}. Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    return Page();
                }
            }

            return RedirectToPage();
        }

        private async Task LogActivity(string action, string details)
        {
            var user = await _userManager.GetUserAsync(User);
            var log = new SystemLog
            {
                // Safety check: ensure user is logged in for the log entry
                UserId = user?.Id ?? "SYSTEM",
                Action = action,
                Details = details,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                CreatedAt = DateTime.Now
            };

            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }

    public class UserDto
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LogDto
    {
        public string Action { get; set; }
        public string Details { get; set; }
        public string UserName { get; set; }
        public string IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
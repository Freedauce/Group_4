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
    [Authorize(Roles = "manager")]
    public class ManagerModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public ManagerModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // Properties for dashboard stats
        public int TotalCars { get; set; }
        public int AvailableCars { get; set; }
        public int PendingBookings { get; set; }
        public int PendingPayments { get; set; }
        public List<Car> Cars { get; set; } = new();
        public List<PendingPaymentDto> PendingPaymentsList { get; set; } = new();

        // System health metrics
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public int ActiveUsers { get; set; }
        public string SystemUptime { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Load statistics
            TotalCars = await _context.Cars.CountAsync();
            AvailableCars = await _context.Cars.CountAsync(c => c.IsAvailable);
            PendingBookings = await _context.Bookings.CountAsync(b => b.Status == "Pending");
            PendingPayments = await _context.Payments.CountAsync(p => p.Status == "Pending");

            // Load cars
            Cars = await _context.Cars.OrderByDescending(c => c.CreatedAt).ToListAsync();

            // Load pending payments with client info - FIXED QUERY
            PendingPaymentsList = await _context.Payments
                .Where(p => p.Status == "Pending")
                .Include(p => p.Booking)
                .ThenInclude(b => b.Client)
                .Select(p => new PendingPaymentDto
                {
                    Id = p.Id,
                    Amount = p.Amount,
                    PaymentCode = p.PaymentCode,
                    PaymentType = p.PaymentType,
                    ClientName = p.Booking.Client.UserName,
                    ClientEmail = p.Booking.Client.Email
                })
                .ToListAsync();

            // Get system health metrics
            await LoadSystemHealthMetrics();

            // Log manager access
            await LogActivity("Manager Dashboard Access", "Manager accessed the dashboard");

            return Page();
        }

        public async Task<IActionResult> OnPostAddCarAsync(string Brand, string Model, int Year, string Color,
            string PlateNumber, string Specifications, decimal DailyRateRWF, IFormFile ImageFile)
        {
            var car = new Car
            {
                Brand = Brand,
                Model = Model,
                Year = Year,
                Color = Color,
                PlateNumber = PlateNumber,
                Specifications = Specifications,
                DailyRateRWF = DailyRateRWF,
                IsAvailable = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // Handle image upload
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "cars");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(fileStream);
                }

                car.ImageUrl = $"/uploads/cars/{uniqueFileName}";
            }

            _context.Cars.Add(car);
            await _context.SaveChangesAsync();

            await LogActivity("Car Added", $"Added car: {Brand} {Model} - {PlateNumber}");

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteCarAsync(int carId)
        {
            var car = await _context.Cars.FindAsync(carId);
            if (car != null)
            {
                _context.Cars.Remove(car);
                await _context.SaveChangesAsync();
                await LogActivity("Car Deleted", $"Deleted car: {car.Brand} {car.Model}");
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostApprovePaymentAsync(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)
                .ThenInclude(b => b.Client)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment != null)
            {
                payment.Status = "Approved";
                payment.ApprovedBy = User.Identity.Name;
                payment.ApprovedAt = DateTime.Now;

                var booking = await _context.Bookings.FindAsync(payment.BookingId);
                if (booking != null)
                {
                    if (payment.PaymentType == "Deposit")
                    {
                        booking.DepositPaid = true;
                        booking.DepositPaidAt = DateTime.Now;
                    }
                    else if (payment.PaymentType == "Full")
                    {
                        booking.FullPaymentPaid = true;
                        booking.FullPaymentPaidAt = DateTime.Now;
                        booking.Status = "Approved";

                        // Add pickup location to booking
                        booking.PickupLocation = "Kimihurura";
                        booking.PickupAddress = "Kigali Cars, 14 KG 690 St, Kigali";
                    }
                }

                await _context.SaveChangesAsync();
                await LogActivity("Payment Approved", $"Approved {payment.PaymentType} payment for {payment.Booking.Client.UserName} - Amount: {payment.Amount} RWF");
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectPaymentAsync(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)
                .ThenInclude(b => b.Client)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment != null)
            {
                payment.Status = "Rejected";
                payment.ApprovedBy = User.Identity.Name;
                payment.ApprovedAt = DateTime.Now;

                // Reset the booking payment status
                var booking = await _context.Bookings.FindAsync(payment.BookingId);
                if (booking != null)
                {
                    if (payment.PaymentType == "Deposit")
                    {
                        booking.DepositPaid = false;
                        booking.DepositPaidAt = null;
                    }
                    else if (payment.PaymentType == "Full")
                    {
                        booking.FullPaymentPaid = false;
                        booking.FullPaymentPaidAt = null;
                    }
                }

                await _context.SaveChangesAsync();
                await LogActivity("Payment Rejected", $"Rejected {payment.PaymentType} payment for {payment.Booking.Client.UserName} - Amount: {payment.Amount} RWF");
            }

            return RedirectToPage();
        }

        private async Task LoadSystemHealthMetrics()
        {
            try
            {
                // CPU Usage
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
                await Task.Delay(100);
                CpuUsage = Math.Round(cpuCounter.NextValue(), 2);

                // Memory Usage
                var ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                MemoryUsage = Math.Round(ramCounter.NextValue(), 2);
            }
            catch
            {
                // Fallback values if performance counters fail
                CpuUsage = 0;
                MemoryUsage = 0;
            }

            // Active Users (users who logged in within the last hour)
            var oneHourAgo = DateTime.Now.AddHours(-1);
            ActiveUsers = await _context.SystemLogs
                .Where(l => l.CreatedAt >= oneHourAgo && l.Action.Contains("Login"))
                .Select(l => l.UserId)
                .Distinct()
                .CountAsync();

            // System Uptime
            var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
            SystemUptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        }

        private async Task LogActivity(string action, string details)
        {
            var userId = _userManager.GetUserId(User);

            var log = new SystemLog
            {
                UserId = userId ?? "SYSTEM",
                Action = action,
                Details = details,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                CreatedAt = DateTime.Now
            };

            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }

    public class PendingPaymentDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string PaymentCode { get; set; }
        public string PaymentType { get; set; }
        public string ClientName { get; set; }
        public string ClientEmail { get; set; }
    }
}
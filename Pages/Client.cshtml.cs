using Exam.Models;
using Exam.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Exam.Pages
{
    [Authorize(Roles = "client")]
    public class ClientModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClientModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<Car> AvailableCars { get; set; } = new();
        public List<BookingDto> MyBookings { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Identity/Account/Login");
            }

            // Load available cars - safely filter out NULL values
            AvailableCars = await _context.Cars
                .Where(c => c.IsAvailable
                    && !string.IsNullOrEmpty(c.Brand)
                    && !string.IsNullOrEmpty(c.Model)
                    && !string.IsNullOrEmpty(c.PlateNumber)
                    && c.DailyRateRWF > 0
                    && c.Year > 0)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Use a safe JOIN query that handles NULL car records
            MyBookings = await (from booking in _context.Bookings
                                join car in _context.Cars on booking.CarId equals car.Id into carJoin
                                from car in carJoin.DefaultIfEmpty() // LEFT JOIN
                                where booking.ClientId == user.Id
                                orderby booking.CreatedAt descending
                                select new BookingDto
                                {
                                    Id = booking.Id,
                                    CarBrand = car != null ? car.Brand : "Car Not Found",
                                    CarModel = car != null ? car.Model : "Car Not Found",
                                    StartDate = booking.StartDate,
                                    EndDate = booking.EndDate,
                                    TotalCars = booking.TotalCars,
                                    TotalAmount = booking.TotalAmount,
                                    DepositAmount = booking.DepositAmount,
                                    DiscountApplied = booking.DiscountApplied,
                                    Status = booking.Status,
                                    PaymentCode = booking.PaymentCode,
                                    DepositPaid = booking.DepositPaid,
                                    FullPaymentPaid = booking.FullPaymentPaid
                                })
                                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostCreateBookingAsync(int CarId, DateTime StartDate, DateTime EndDate, int TotalCars)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Identity/Account/Login");
            }

            var car = await _context.Cars.FindAsync(CarId);

            if (car == null || !car.IsAvailable)
            {
                ModelState.AddModelError("", "Car is not available");
                return Page();
            }

            // Validate dates
            if (StartDate < DateTime.Today)
            {
                ModelState.AddModelError("", "Start date cannot be in the past");
                return Page();
            }

            if (EndDate <= StartDate)
            {
                ModelState.AddModelError("", "End date must be after start date");
                return Page();
            }

            // Calculate booking details
            var days = (EndDate - StartDate).Days + 1;
            var subtotal = days * car.DailyRateRWF * TotalCars;
            var discountApplied = TotalCars >= 3;
            var discount = discountApplied ? subtotal * 0.2m : 0;
            var totalAmount = subtotal - discount;
            var depositAmount = totalAmount * 0.2m;

            // Generate unique payment code
            var paymentCode = $"PAY{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";

            var booking = new Booking
            {
                CarId = CarId,
                ClientId = user.Id,
                StartDate = StartDate,
                EndDate = EndDate,
                TotalCars = TotalCars,
                TotalAmount = totalAmount,
                DepositAmount = depositAmount,
                DiscountApplied = discountApplied,
                Status = "Pending",
                PaymentCode = paymentCode,
                DepositPaid = false,
                FullPaymentPaid = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Bookings.Add(booking);

            // Mark car as unavailable
            car.IsAvailable = false;
            car.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Log activity
            await LogActivity("Booking Created", $"Created booking for {car.Brand} {car.Model}");

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostConfirmPaymentAsync(int bookingId, string paymentType)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null)
            {
                return NotFound();
            }

            var amount = paymentType == "Deposit" ? booking.DepositAmount : (booking.TotalAmount - booking.DepositAmount);

            // Create payment record
            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = amount,
                PaymentType = paymentType,
                PaymentCode = booking.PaymentCode,
                Status = "Pending",
                PaidAt = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            _context.Payments.Add(payment);

            // Update booking payment status
            if (paymentType == "Deposit")
            {
                booking.DepositPaid = true;
                booking.DepositPaidAt = DateTime.Now;
            }
            else if (paymentType == "Full")
            {
                booking.FullPaymentPaid = true;
                booking.FullPaymentPaidAt = DateTime.Now;
                booking.Status = "Approved";
            }

            await _context.SaveChangesAsync();

            // Log activity
            await LogActivity("Payment Confirmed", $"Confirmed {paymentType} payment for booking #{bookingId}");

            return RedirectToPage();
        }

        private async Task LogActivity(string action, string details)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var log = new SystemLog
                {
                    UserId = user.Id,
                    Action = action,
                    Details = details,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    CreatedAt = DateTime.Now
                };

                _context.SystemLogs.Add(log);
                await _context.SaveChangesAsync();
            }
        }
    }

    public class BookingDto
    {
        public int Id { get; set; }
        public string CarBrand { get; set; }
        public string CarModel { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalCars { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public bool DiscountApplied { get; set; }
        public string Status { get; set; }
        public string PaymentCode { get; set; }
        public bool DepositPaid { get; set; }
        public bool FullPaymentPaid { get; set; }
        public string PickupLocation { get; set; }
        public string PickupAddress { get; set; }
    }
}
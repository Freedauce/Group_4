using System.ComponentModel.DataAnnotations;

namespace Exam.Models
{
    public class Payment
    {
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        // Navigation property - REQUIRED for the relationship to work
        public virtual Booking Booking { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [StringLength(50)]
        public string PaymentType { get; set; }

        [StringLength(100)]
        public string PaymentCode { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        public DateTime? PaidAt { get; set; }

        public string ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
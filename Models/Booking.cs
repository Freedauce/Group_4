using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Exam.Models
{
    public class Booking
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CarId { get; set; }

        [ForeignKey("CarId")]
        public virtual Car Car { get; set; }

        [Required]
        public string ClientId { get; set; }

        [ForeignKey("ClientId")]
        public virtual ApplicationUser Client { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public int TotalCars { get; set; } = 1;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositAmount { get; set; }

        public bool DiscountApplied { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } // Pending, Approved, Rejected, Completed

        [Required]
        [MaxLength(100)]
        public string PaymentCode { get; set; }

        public bool DepositPaid { get; set; }
        public DateTime? DepositPaidAt { get; set; }

        public bool FullPaymentPaid { get; set; }
        public DateTime? FullPaymentPaidAt { get; set; }

        // NEW: Pickup location fields
        [MaxLength(200)]
        public string PickupLocation { get; set; }

        [MaxLength(500)]
        public string PickupAddress { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public virtual ICollection<Payment> Payments { get; set; }
    }
}
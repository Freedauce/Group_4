using System.ComponentModel.DataAnnotations;

namespace Exam.Models
{
    public class Car
    {
        public int Id { get; set; }

        [Required(AllowEmptyStrings = true)]
        [StringLength(100)]
        public string Brand { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = true)]
        [StringLength(100)]
        public string Model { get; set; } = string.Empty;

        [Required]
        public int Year { get; set; }

        [StringLength(50)]
        public string Color { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = true)]
        [StringLength(20)]
        public string PlateNumber { get; set; } = string.Empty;

        public string Specifications { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal DailyRateRWF { get; set; }

        public bool IsAvailable { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
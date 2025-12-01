using System.ComponentModel.DataAnnotations;

namespace Exam.Models
{
    public class SystemLog
    {
        public int Id { get; set; }

        [StringLength(450)] // Match IdentityUser Id length
        public string UserId { get; set; } // Make this optional

        [Required]
        [StringLength(100)]
        public string Action { get; set; }

        public string Details { get; set; }

        [StringLength(50)]
        public string IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
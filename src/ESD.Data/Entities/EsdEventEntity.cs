using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESD.Data.Entities;

[Table("EsdEvents")]
public sealed class EsdEventEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public DateTime EventTime { get; set; }

    [Required]
    [MaxLength(100)]
    public string DeviceName { get; set; } = "";

    [MaxLength(100)]
    public string? EmployeeId { get; set; }

    [Required]
    [MaxLength(60)]
    public string EventType { get; set; } = "";

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = "";

    [MaxLength(500)]
    public string? RawData { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }
}

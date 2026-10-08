using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.Models;

public class Product
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Balance { get; set; }
}

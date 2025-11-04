using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace API.Models;

[Index(nameof(ValidFrom), IsUnique = false)]
[Index(nameof(Owner), IsUnique = false)]
public sealed class PowerPlant
{
    [Key]
    public Guid Id { get; init; }
    [Required]
    public decimal Power { get; init; }
    [Required, MaxLength(200)]
    public required string Owner { get; init; }
    [Required]
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
}
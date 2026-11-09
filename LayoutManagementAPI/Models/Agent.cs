using System.ComponentModel.DataAnnotations;

namespace LayoutManagementAPI.Models;

public class Agent
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(200)]
    public string Email { get; set; } = "";

    [Required, MaxLength(30)]
    public string Phone { get; set; } = "";

    [Required, MaxLength(40)]
    public string LicenseNumber { get; set; } = "";

    [MaxLength(256)]
    public string CreatedBy { get; set; } = "";

    public List<PropertyListing> Properties { get; set; } = new();
}

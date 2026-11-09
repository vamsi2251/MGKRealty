using System.ComponentModel.DataAnnotations;

namespace LayoutManagementAPI.Models;

public class Client
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(200)]
    public string Email { get; set; } = "";

    [Required, MaxLength(30)]
    public string Phone { get; set; } = "";

    public ClientType ClientType { get; set; }

    [MaxLength(256)]
    public string CreatedBy { get; set; } = "";

    public List<Inquiry> Inquiries { get; set; } = new();
}

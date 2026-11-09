namespace LayoutManagementWebApp.Models;

public class AuthResponse
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public DateTime ExpiresOn { get; set; }
}

public class StoredAuth
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public DateTime ExpiresOn { get; set; }
}

public class RegisterModel
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(80)]
    public string FullName { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(60)]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MinLength(8)]
    public string Password { get; set; } = "";
}

public class LoginModel
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    public string Password { get; set; } = "";
}

public class UserModel
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public bool LoginRestricted { get; set; }
    public string Role { get; set; } = "User";
    public bool IsBuiltInAdmin { get; set; }
    public string NewPassword { get; set; } = "";
    public string OriginalUsername { get; set; } = "";
}

public class LayoutCatalogItemModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class UserSketchGrantModel
{
    public int LayoutId { get; set; }
    public string Level { get; set; } = "ReadOnly";
}

public class LayoutPartnerModel
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Level { get; set; } = "ReadOnly";
}

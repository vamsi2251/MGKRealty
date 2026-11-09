using LayoutManagementAPI.Data;
using Xunit;

namespace LayoutManagement.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = PasswordHasher.Hash("Sup3rSecret!");
        Assert.True(PasswordHasher.Verify("Sup3rSecret!", hash));
    }

    [Fact]
    public void Verify_fails_for_wrong_password()
    {
        var hash = PasswordHasher.Hash("Sup3rSecret!");
        Assert.False(PasswordHasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_is_salted_so_two_hashes_of_same_password_differ()
    {
        var a = PasswordHasher.Hash("same-password");
        var b = PasswordHasher.Hash("same-password");

        Assert.NotEqual(a, b);
        Assert.True(PasswordHasher.Verify("same-password", a));
        Assert.True(PasswordHasher.Verify("same-password", b));
    }

    [Fact]
    public void Stored_hash_has_three_parts_and_does_not_contain_plaintext()
    {
        var hash = PasswordHasher.Hash("pla1ntext-value");

        Assert.Equal(3, hash.Split('.').Length);
        Assert.DoesNotContain("pla1ntext-value", hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("abc.def")]
    [InlineData("10.not_base64!.not_base64!")]
    public void Verify_returns_false_for_malformed_or_empty_stored(string stored)
    {
        Assert.False(PasswordHasher.Verify("anything", stored));
    }

    [Fact]
    public void Verify_returns_false_for_empty_password()
    {
        var hash = PasswordHasher.Hash("real-password");
        Assert.False(PasswordHasher.Verify("", hash));
    }
}

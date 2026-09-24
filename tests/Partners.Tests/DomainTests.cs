using BuildingBlocks.ServiceDefaults.Errors;
using Partners.Api.Domain;

namespace Partners.Tests;

public class UserIdTests
{
    [Theory]
    [InlineData("u1")]
    [InlineData("User_42")]
    [InlineData("a")]
    public void Accepts_valid_identifiers(string value) => UserId.Create(value).Value.Should().Be(value);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("a-b")]
    [InlineData("with space")]
    public void Rejects_invalid_identifiers(string? value) =>
        FluentActions.Invoking(() => UserId.Create(value)).Should().Throw<ValidationException>();

    [Fact]
    public void Enforces_max_length()
    {
        UserId.Create(new string('a', 64)).Value.Should().HaveLength(64);
        FluentActions.Invoking(() => UserId.Create(new string('a', 65))).Should().Throw<ValidationException>();
    }
}

public class TreePathTests
{
    [Fact]
    public void Root_has_no_ancestors() =>
        TreePath.Root(UserId.Create("root")).Ancestors().Should().BeEmpty();

    [Fact]
    public void Ancestors_are_ordered_from_direct_parent_to_root()
    {
        var path = TreePath.Parse("root.a.b.c");

        path.Ancestors().Should().Equal(
            new Ancestor("b", 1),
            new Ancestor("a", 2),
            new Ancestor("root", 3));
    }

    [Fact]
    public void Child_appends_label()
    {
        var path = TreePath.Parse("root.a").Child(UserId.Create("b"));

        path.Value.Should().Be("root.a.b");
        path.Owner.Should().Be("b");
        path.Depth.Should().Be(3);
    }

    [Theory]
    [InlineData("root.a.b", "root.a", true)]
    [InlineData("root.a", "root.a", true)]
    [InlineData("root.ab", "root.a", false)]
    [InlineData("root", "root.a", false)]
    [InlineData("other.a", "root.a", false)]
    public void Detects_self_or_descendant(string candidate, string of, bool expected) =>
        TreePath.Parse(candidate).IsSelfOrDescendantOf(TreePath.Parse(of)).Should().Be(expected);
}

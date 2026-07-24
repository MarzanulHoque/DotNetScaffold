using DotNetScaffold.Abstractions;
using FluentAssertions;

namespace DotNetScaffold.Metadata.Tests;

/// <summary>
/// Exercises the real EF Core model-reading pipeline (assembly loading, reflection-based DbContext
/// construction, IModel traversal) against samples/SampleBlog -- a fixture chosen specifically to cover
/// one-to-many (Author-Post), one-to-one (Post-PostDetail), self-referencing (Category-ParentCategory),
/// and many-to-many (Post-Tag) relationships, per SRS 3.2.2/3.2.3.
///
/// SampleBlog is referenced via &lt;ProjectReference&gt; purely so it's always built fresh as a build-order
/// dependency of this test project -- its build output DLL, copied into this project's own output
/// directory, is what's actually loaded at runtime (via AppContext.BaseDirectory), the same way the real
/// tool will load an arbitrary target project's output. This test project never references SampleBlog's
/// types directly in code.
/// </summary>
public class EfModelReaderTests
{
    private const string DbContextTypeName = "SampleBlog.SampleBlogContext";

    private readonly string _assemblyPath = Path.Combine(AppContext.BaseDirectory, "SampleBlog.dll");
    private readonly EfModelReader _reader = new(new PluginAssemblyLoader());

    [Fact]
    public void ReadModel_ReturnsOnlyRealEntities_ExcludingTheImplicitManyToManyJoinEntity()
    {
        var model = _reader.ReadModel(_assemblyPath, DbContextTypeName);

        model.Entities.Select(e => e.ClrName).Should().BeEquivalentTo(
            "Author", "Category", "Post", "PostDetail", "Tag");
    }

    [Fact]
    public void ReadModel_ReadsScalarPropertiesWithNullabilityAndMaxLength()
    {
        var model = _reader.ReadModel(_assemblyPath, DbContextTypeName);
        var author = model.Entities.Single(e => e.ClrName == "Author");

        author.Properties.Should().ContainSingle(p => p.Name == "Name")
            .Which.Should().BeEquivalentTo(new { ClrTypeName = "string", IsNullable = false, MaxLength = 200, IsPrimaryKey = false });

        author.Properties.Should().ContainSingle(p => p.Name == "Email")
            .Which.IsNullable.Should().BeTrue();

        author.PrimaryKey.Name.Should().Be("Id");
        author.IsCompositeKey.Should().BeFalse();
    }

    [Fact]
    public void ReadModel_ReadsOneToManyRelationship_AuthorToPost()
    {
        var model = _reader.ReadModel(_assemblyPath, DbContextTypeName);
        var author = model.Entities.Single(e => e.ClrName == "Author");
        var post = model.Entities.Single(e => e.ClrName == "Post");

        author.Navigations.Should().ContainSingle(n => n.PropertyName == "Posts")
            .Which.Should().BeEquivalentTo(new
            {
                IsCollection = true,
                RelatedEntityClrName = "Post",
                Kind = RelationshipKind.OneToMany,
                IsSelfReferencing = false,
                ForeignKeyPropertyName = (string?)null, // the collection side never holds the FK itself
            });

        post.Navigations.Should().ContainSingle(n => n.PropertyName == "Author")
            .Which.Should().BeEquivalentTo(new
            {
                IsCollection = false,
                RelatedEntityClrName = "Author",
                Kind = RelationshipKind.OneToMany,
                IsSelfReferencing = false,
                IsRequired = true,
                ForeignKeyPropertyName = "AuthorId",
            });
    }

    [Fact]
    public void ReadModel_ReadsOneToOneRelationship_PostToPostDetail()
    {
        var model = _reader.ReadModel(_assemblyPath, DbContextTypeName);
        var post = model.Entities.Single(e => e.ClrName == "Post");
        var postDetail = model.Entities.Single(e => e.ClrName == "PostDetail");

        post.Navigations.Should().ContainSingle(n => n.PropertyName == "PostDetail")
            .Which.Kind.Should().Be(RelationshipKind.OneToOne);

        postDetail.Navigations.Should().ContainSingle(n => n.PropertyName == "Post")
            .Which.Should().BeEquivalentTo(new
            {
                IsCollection = false,
                Kind = RelationshipKind.OneToOne,
                IsRequired = true,
                ForeignKeyPropertyName = "PostId",
            });
    }

    [Fact]
    public void ReadModel_ReadsSelfReferencingRelationship_CategoryParentChild()
    {
        var model = _reader.ReadModel(_assemblyPath, DbContextTypeName);
        var category = model.Entities.Single(e => e.ClrName == "Category");

        category.Navigations.Should().ContainSingle(n => n.PropertyName == "ParentCategory")
            .Which.Should().BeEquivalentTo(new
            {
                IsCollection = false,
                RelatedEntityClrName = "Category",
                Kind = RelationshipKind.OneToMany,
                IsSelfReferencing = true,
                IsRequired = false,
                ForeignKeyPropertyName = "ParentCategoryId",
            });

        category.Navigations.Should().ContainSingle(n => n.PropertyName == "ChildCategories")
            .Which.Should().BeEquivalentTo(new
            {
                IsCollection = true,
                RelatedEntityClrName = "Category",
                IsSelfReferencing = true,
            });
    }

    [Fact]
    public void ReadModel_DetectsManyToManyAndRecordsItAsSkipped_NotAsARegularNavigation()
    {
        var model = _reader.ReadModel(_assemblyPath, DbContextTypeName);
        var post = model.Entities.Single(e => e.ClrName == "Post");
        var tag = model.Entities.Single(e => e.ClrName == "Tag");

        post.Navigations.Should().NotContain(n => n.PropertyName == "Tags");
        tag.Navigations.Should().NotContain(n => n.PropertyName == "Posts");

        model.SkippedManyToMany.Should().BeEquivalentTo(
        [
            new ManyToManySkip("Post", "Tags", "Tag"),
            new ManyToManySkip("Tag", "Posts", "Post"),
        ]);
    }

    [Fact]
    public void ReadModel_WhenDbContextTypeNameIsWrong_ThrowsWithAClearMessage()
    {
        var act = () => _reader.ReadModel(_assemblyPath, "SampleBlog.NoSuchContext");

        act.Should().Throw<InvalidOperationException>().WithMessage("*was not found*");
    }

    [Fact]
    public void ReadModel_WhenTypeIsNotADbContext_ThrowsWithAClearMessage()
    {
        var act = () => _reader.ReadModel(_assemblyPath, "SampleBlog.Author");

        act.Should().Throw<InvalidOperationException>().WithMessage("*does not derive from*DbContext*");
    }
}

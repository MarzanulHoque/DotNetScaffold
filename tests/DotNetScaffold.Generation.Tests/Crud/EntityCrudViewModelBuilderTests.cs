using DotNetScaffold.Abstractions;
using DotNetScaffold.Generation.Crud;
using FluentAssertions;

namespace DotNetScaffold.Generation.Tests.Crud;

/// <summary>
/// Hand-builds <see cref="EntityMetadata"/>/<see cref="DbContextModelMetadata"/> directly, the same
/// approach as <c>Dtos.EntityDtoViewModelBuilderTests</c> -- these tests pin down
/// <see cref="EntityCrudViewModelBuilder"/>'s own additions (PK info, sample-value literals, and the
/// richer per-navigation shape M5's service/test templates need) rather than re-testing the flattening
/// rule itself, which is covered once by <see cref="ReferenceNavigationFlattener"/>'s shared use in
/// <c>Dtos.EntityDtoViewModelBuilderTests</c>.
/// </summary>
public class EntityCrudViewModelBuilderTests
{
    private readonly EntityCrudViewModelBuilder _builder = new();

    [Fact]
    public void Build_ExposesPrimaryKeyInfoAndExcludesItFromCreateOrUpdateProperties()
    {
        var author = new EntityMetadata(
            "Author", "Blog.Author", "Blog",
            [
                new PropertyMetadata("Id", "int", false, null, true, false),
                new PropertyMetadata("Name", "string", false, 200, false, false),
                new PropertyMetadata("Email", "string", true, null, false, false),
            ],
            []);
        var model = new DbContextModelMetadata("Blog.BlogContext", [author], []);

        var viewModel = _builder.Build(author, model);

        viewModel.PrimaryKeyPropertyName.Should().Be("Id");
        viewModel.PrimaryKeyCSharpTypeName.Should().Be("int");
        viewModel.PrimaryKeySampleValueLiteral.Should().Be("1");

        viewModel.ScalarProperties.Should().BeEquivalentTo(
        [
            new CrudPropertyViewModel("Id", "int", "1"),
            new CrudPropertyViewModel("Name", "string", "\"Test\""),
            new CrudPropertyViewModel("Email", "string?", "\"Test\""),
        ]);

        viewModel.CreateOrUpdateProperties.Should().BeEquivalentTo(
        [
            new CrudPropertyViewModel("Name", "string", "\"Test\""),
            new CrudPropertyViewModel("Email", "string?", "\"Test\""),
        ]);
    }

    [Fact]
    public void Build_FlattensARequiredReferenceNavigation_WithNavigationAndDisplayInfoForCodegen()
    {
        var author = new EntityMetadata(
            "Author", "Blog.Author", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("Name", "string", false, 200, false, false)],
            []);
        var post = new EntityMetadata(
            "Post", "Blog.Post", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("AuthorId", "int", false, null, false, true)],
            [new NavigationMetadata("Author", false, "Author", RelationshipKind.OneToMany, false, true, "AuthorId")]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [author, post], []);

        var viewModel = _builder.Build(post, model);

        viewModel.ReferenceNavigations.Should().BeEquivalentTo(
        [
            new CrudReferenceNavigationViewModel("Author", "Author", "AuthorName", "string", false, "Name", "\"Test\""),
        ]);
    }

    [Fact]
    public void Build_PrincipalSideOfOneToOneWithNoDisplayProperty_FallsBackToTheRelatedRowsPrimaryKey()
    {
        var postDetail = new EntityMetadata("PostDetail", "Blog.PostDetail", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("PostId", "int", false, null, false, true),
             new PropertyMetadata("ViewCount", "int", false, null, false, false)],
            [new NavigationMetadata("Post", false, "Post", RelationshipKind.OneToOne, false, true, "PostId")]);
        var post = new EntityMetadata("Post", "Blog.Post", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false)],
            [new NavigationMetadata("PostDetail", false, "PostDetail", RelationshipKind.OneToOne, false, true, null)]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [postDetail, post], []);

        var viewModel = _builder.Build(post, model);

        viewModel.ReferenceNavigations.Should().BeEquivalentTo(
        [
            new CrudReferenceNavigationViewModel("PostDetail", "PostDetail", "PostDetailId", "int?", true, "Id", "1"),
        ]);
    }

    [Fact]
    public void Build_RendersAChildCollection_WithTheChildsOwnScalarAndReferenceShapeForInlineMapping()
    {
        var category = new EntityMetadata("Category", "Blog.Category", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("Name", "string", false, 100, false, false)],
            []);
        var post = new EntityMetadata("Post", "Blog.Post", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("Title", "string", false, 300, false, false),
             new PropertyMetadata("CategoryId", "int", true, null, false, true)],
            [new NavigationMetadata("Category", false, "Category", RelationshipKind.OneToMany, false, false, "CategoryId")]);
        var author = new EntityMetadata("Author", "Blog.Author", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false)],
            [new NavigationMetadata("Posts", true, "Post", RelationshipKind.OneToMany, false, true, null)]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [category, post, author], []);

        var viewModel = _builder.Build(author, model);

        viewModel.ChildCollections.Should().HaveCount(1);
        var childCollection = viewModel.ChildCollections[0];
        childCollection.NavigationPropertyName.Should().Be("Posts");
        childCollection.ChildEntityClrName.Should().Be("Post");
        childCollection.ChildListDtoTypeName.Should().Be("PostListDto");
        childCollection.ChildScalarProperties.Should().Contain(new CrudPropertyViewModel("Title", "string", "\"Test\""));
        childCollection.ChildReferenceNavigations.Should().BeEquivalentTo(
        [
            new CrudReferenceNavigationViewModel("Category", "Category", "CategoryName", "string?", true, "Name", "\"Test\""),
        ]);
    }

    [Fact]
    public void Build_WhenNoDisplayPropertyAndNoFallback_OmitsTheReferenceNavigationEntirely()
    {
        var thing = new EntityMetadata(
            "Thing", "Blog.Thing", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("Quantity", "int", false, null, false, false)],
            []);
        var owner = new EntityMetadata(
            "Owner", "Blog.Owner", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("ThingId", "int", false, null, false, true)],
            [new NavigationMetadata("Thing", false, "Thing", RelationshipKind.OneToMany, false, true, "ThingId")]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [thing, owner], []);

        var viewModel = _builder.Build(owner, model);

        viewModel.ReferenceNavigations.Should().BeEmpty();
    }
}

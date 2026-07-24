using DotNetScaffold.Abstractions;
using DotNetScaffold.Generation.Dtos;
using FluentAssertions;

namespace DotNetScaffold.Generation.Tests.Dtos;

/// <summary>
/// Hand-builds <see cref="EntityMetadata"/>/<see cref="DbContextModelMetadata"/> directly rather than
/// going through EF Core -- these tests are about the flattening/nullability rules in
/// <see cref="EntityDtoViewModelBuilder"/> themselves, which is easiest to pin down precisely with
/// fully-controlled inputs rather than whatever a real model happens to produce.
/// </summary>
public class EntityDtoViewModelBuilderTests
{
    private readonly EntityDtoViewModelBuilder _builder = new();

    [Fact]
    public void Build_IncludesAllScalarProperties_OnBothScalarAndCreateUpdateLists()
    {
        var author = new EntityMetadata(
            "Author", "Blog.Author", "Blog",
            Properties:
            [
                new PropertyMetadata("Id", "int", IsNullable: false, MaxLength: null, IsPrimaryKey: true, IsForeignKey: false),
                new PropertyMetadata("Name", "string", IsNullable: false, MaxLength: 200, IsPrimaryKey: false, IsForeignKey: false),
                new PropertyMetadata("Email", "string", IsNullable: true, MaxLength: null, IsPrimaryKey: false, IsForeignKey: false),
            ],
            Navigations: []);
        var model = new DbContextModelMetadata("Blog.BlogContext", [author], []);

        var viewModel = _builder.Build(author, model, "MyApp.BLL");

        viewModel.Namespace.Should().Be("MyApp.BLL");
        viewModel.EntityName.Should().Be("Author");
        viewModel.ScalarProperties.Should().BeEquivalentTo(
        [
            new DtoPropertyViewModel("Id", "int"),
            new DtoPropertyViewModel("Name", "string"),
            new DtoPropertyViewModel("Email", "string?"),
        ]);

        // Id (the PK) is excluded from create/update; Name/Email are not.
        viewModel.CreateProperties.Should().BeEquivalentTo(
        [
            new DtoPropertyViewModel("Name", "string"),
            new DtoPropertyViewModel("Email", "string?"),
        ]);
        viewModel.UpdateProperties.Should().BeEquivalentTo(viewModel.CreateProperties);
    }

    [Fact]
    public void Build_FlattensARequiredReferenceNavigation_AsANonNullableField()
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
            [new NavigationMetadata("Author", IsCollection: false, "Author", RelationshipKind.OneToMany,
                IsSelfReferencing: false, IsRequired: true, ForeignKeyPropertyName: "AuthorId")]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [author, post], []);

        var viewModel = _builder.Build(post, model, "MyApp.BLL");

        viewModel.FlattenedReferenceProperties.Should().BeEquivalentTo(
        [
            new DtoPropertyViewModel("AuthorName", "string"), // required relationship -> non-nullable
        ]);
        // The raw FK scalar is still present too, independent of the flattened field.
        viewModel.ScalarProperties.Should().Contain(new DtoPropertyViewModel("AuthorId", "int"));
    }

    [Fact]
    public void Build_FlattensAnOptionalReferenceNavigation_AsANullableFieldEvenIfDisplayPropertyIsNot()
    {
        var category = new EntityMetadata(
            "Category", "Blog.Category", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("Name", "string", false, 100, false, false)],
            []);
        var post = new EntityMetadata(
            "Post", "Blog.Post", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("CategoryId", "int", true, null, false, true)],
            [new NavigationMetadata("Category", false, "Category", RelationshipKind.OneToMany,
                false, IsRequired: false, "CategoryId")]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [category, post], []);

        var viewModel = _builder.Build(post, model, "MyApp.BLL");

        viewModel.FlattenedReferenceProperties.Should().BeEquivalentTo(
        [
            new DtoPropertyViewModel("CategoryName", "string?"), // optional relationship -> nullable
        ]);
    }

    [Fact]
    public void Build_WhenNoDisplayPropertyExistsOnRelatedEntity_OmitsTheFlattenedFieldEntirely()
    {
        var thing = new EntityMetadata(
            "Thing", "Blog.Thing", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("Quantity", "int", false, null, false, false)], // no string property at all
            []);
        var owner = new EntityMetadata(
            "Owner", "Blog.Owner", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("ThingId", "int", false, null, false, true)],
            [new NavigationMetadata("Thing", false, "Thing", RelationshipKind.OneToMany, false, true, "ThingId")]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [thing, owner], []);

        var viewModel = _builder.Build(owner, model, "MyApp.BLL");

        viewModel.FlattenedReferenceProperties.Should().BeEmpty();
        // The raw FK scalar (ThingId) is still there -- it's simply not redundantly flattened too.
        viewModel.ScalarProperties.Should().Contain(p => p.Name == "ThingId");
    }

    [Fact]
    public void Build_PrincipalSideOfOneToOneWithNoDisplayProperty_FallsBackToTheRelatedRowsPrimaryKey()
    {
        // Mirrors Post -> PostDetail: PostDetail has no string property at all, and Post (the principal
        // side) doesn't hold the FK itself -- PostDetail does (PostDetail.PostId). Without a fallback,
        // Post's DTO would give zero indication a PostDetail even exists.
        var postDetail = new EntityMetadata("PostDetail", "Blog.PostDetail", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("PostId", "int", false, null, false, true),
             new PropertyMetadata("ViewCount", "int", false, null, false, false)],
            [new NavigationMetadata("Post", false, "Post", RelationshipKind.OneToOne, false, true, "PostId")]);
        var post = new EntityMetadata("Post", "Blog.Post", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false)],
            [new NavigationMetadata("PostDetail", false, "PostDetail", RelationshipKind.OneToOne,
                false, IsRequired: true, ForeignKeyPropertyName: null)]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [postDetail, post], []);

        var viewModel = _builder.Build(post, model, "MyApp.BLL");

        viewModel.FlattenedReferenceProperties.Should().BeEquivalentTo(
        [
            // Nullable even though the navigation is "required" -- a Post can exist with no PostDetail
            // row created yet; IsRequired only describes PostDetail.PostId, not Post's own guarantee.
            new DtoPropertyViewModel("PostDetailId", "int?"),
        ]);
    }

    [Fact]
    public void Build_RendersACollectionNavigation_AsAListOfTheChildsListDto_NeverItsFullDetailDto()
    {
        var post = new EntityMetadata("Post", "Blog.Post", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false)], []);
        var author = new EntityMetadata("Author", "Blog.Author", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false)],
            [new NavigationMetadata("Posts", IsCollection: true, "Post", RelationshipKind.OneToMany,
                false, true, ForeignKeyPropertyName: null)]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [post, author], []);

        var viewModel = _builder.Build(author, model, "MyApp.BLL");

        viewModel.ChildCollections.Should().BeEquivalentTo(
        [
            new DtoCollectionViewModel("Posts", "PostListDto"),
        ]);
    }

    [Fact]
    public void Build_SelfReferencingEntity_FlattensTheReferenceAndListsTheCollectionAsItsOwnListDto()
    {
        var category = new EntityMetadata("Category", "Blog.Category", "Blog",
            [new PropertyMetadata("Id", "int", false, null, true, false),
             new PropertyMetadata("Name", "string", false, 100, false, false),
             new PropertyMetadata("ParentCategoryId", "int", true, null, false, true)],
            [
                new NavigationMetadata("ParentCategory", false, "Category", RelationshipKind.OneToMany,
                    IsSelfReferencing: true, IsRequired: false, "ParentCategoryId"),
                new NavigationMetadata("ChildCategories", true, "Category", RelationshipKind.OneToMany,
                    IsSelfReferencing: true, IsRequired: true, null),
            ]);
        var model = new DbContextModelMetadata("Blog.BlogContext", [category], []);

        var viewModel = _builder.Build(category, model, "MyApp.BLL");

        viewModel.FlattenedReferenceProperties.Should().BeEquivalentTo(
        [
            new DtoPropertyViewModel("ParentCategoryName", "string?"),
        ]);
        viewModel.ChildCollections.Should().BeEquivalentTo(
        [
            new DtoCollectionViewModel("ChildCategories", "CategoryListDto"),
        ]);
    }
}

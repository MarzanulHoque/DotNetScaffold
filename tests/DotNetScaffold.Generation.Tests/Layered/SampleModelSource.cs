namespace DotNetScaffold.Generation.Tests.Layered;

/// <summary>
/// Hand-authored source for a SampleBlog-shaped model (one-to-many, one-to-one, self-referencing, and an
/// intentional many-to-many -- the same relationship coverage as <c>samples/SampleBlog</c>), written
/// directly into a freshly-scaffolded solution's DAL project so <see cref="LayeredCrudGeneratorTests"/> has
/// a real, buildable target to read metadata from and generate CRUD against.
/// </summary>
internal static class SampleModelSource
{
    internal static string Author(string solutionName) => $$"""
        namespace {{solutionName}}.DAL;

        public class Author
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Email { get; set; }
            public ICollection<Post> Posts { get; set; } = new List<Post>();
        }
        """;

    internal static string Post(string solutionName) => $$"""
        namespace {{solutionName}}.DAL;

        public class Post
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Content { get; set; }
            public int AuthorId { get; set; }
            public Author Author { get; set; } = null!;
            public int? CategoryId { get; set; }
            public Category? Category { get; set; }
            public PostDetail? PostDetail { get; set; }
            public ICollection<Tag> Tags { get; set; } = new List<Tag>();
        }
        """;

    internal static string PostDetail(string solutionName) => $$"""
        namespace {{solutionName}}.DAL;

        public class PostDetail
        {
            public int Id { get; set; }
            public int PostId { get; set; }
            public Post Post { get; set; } = null!;
            public int ViewCount { get; set; }
        }
        """;

    internal static string Category(string solutionName) => $$"""
        namespace {{solutionName}}.DAL;

        public class Category
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int? ParentCategoryId { get; set; }
            public Category? ParentCategory { get; set; }
            public ICollection<Category> ChildCategories { get; set; } = new List<Category>();
            public ICollection<Post> Posts { get; set; } = new List<Post>();
        }
        """;

    internal static string Tag(string solutionName) => $$"""
        namespace {{solutionName}}.DAL;

        public class Tag
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public ICollection<Post> Posts { get; set; } = new List<Post>();
        }
        """;

    internal static string AppDbContext(string solutionName) => $$"""
        using Microsoft.EntityFrameworkCore;

        namespace {{solutionName}}.DAL;

        public class AppDbContext : DbContext
        {
            public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
            {
            }

            public DbSet<Author> Authors => Set<Author>();
            public DbSet<Post> Posts => Set<Post>();
            public DbSet<PostDetail> PostDetails => Set<PostDetail>();
            public DbSet<Category> Categories => Set<Category>();
            public DbSet<Tag> Tags => Set<Tag>();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Post>()
                    .HasOne(p => p.PostDetail)
                    .WithOne(d => d.Post)
                    .HasForeignKey<PostDetail>(d => d.PostId)
                    .IsRequired();

                modelBuilder.Entity<Category>()
                    .HasOne(c => c.ParentCategory)
                    .WithMany(c => c.ChildCategories)
                    .HasForeignKey(c => c.ParentCategoryId)
                    .IsRequired(false);
            }
        }
        """;
}

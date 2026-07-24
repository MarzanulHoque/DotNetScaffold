using Microsoft.EntityFrameworkCore;

namespace SampleBlog;

public class SampleBlogContext : DbContext
{
    public SampleBlogContext(DbContextOptions<SampleBlogContext> options) : base(options)
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

using System.ComponentModel.DataAnnotations;

namespace SampleBlog;

public class Post
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Content { get; set; }

    // One-to-many: Post -> Author (required FK)
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;

    // One-to-many: Post -> Category (optional FK)
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    // One-to-one: Post -> PostDetail
    public PostDetail? PostDetail { get; set; }

    // Many-to-many: Post <-> Tag
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}

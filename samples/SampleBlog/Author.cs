using System.ComponentModel.DataAnnotations;

namespace SampleBlog;

public class Author
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    // One-to-many: Author -> Posts
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

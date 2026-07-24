using System.ComponentModel.DataAnnotations;

namespace SampleBlog;

public class Tag
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    // Many-to-many: Tag <-> Post (implicit join entity, via EF Core's skip-navigation convention).
    // Deliberately out of v1 scope (SRS 3.2.3) -- exists so the metadata reader can prove it's
    // detected and skipped-with-a-warning, not silently ignored or mishandled.
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

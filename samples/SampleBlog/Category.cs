using System.ComponentModel.DataAnnotations;

namespace SampleBlog;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Self-referencing one-to-many: a Category may have a parent, and many children.
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> ChildCategories { get; set; } = new List<Category>();

    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

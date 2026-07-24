namespace SampleBlog;

public class PostDetail
{
    public int Id { get; set; }

    // One-to-one: PostDetail -> Post (required, unique FK)
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    public int ViewCount { get; set; }
}

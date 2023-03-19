namespace Backend.Api.GraphQL;

public class Query
{
    public Book GetBook() =>
        new Book
        {
            Title = "Some Book title",
            Author = new()
            {
                Name = "Some Author Name"
            }
        };
}

public record Book
{
    public string Title { get; set; } = null!;
    public Author Author { get; set; } = null!;
}

public record Author
{
    public string Name { get; set; } = null!;
}
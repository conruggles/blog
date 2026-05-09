namespace blog.Models;

public sealed class BlogPost
{
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required DateOnly PublishedOn { get; init; }
    public required string ContentPath { get; init; }

    public string RoutePath => $"/{PublishedOn.Year}/{PublishedOn.Month}/{PublishedOn.Day}/{Slug}.html";
}

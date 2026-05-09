using blog.Models;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace blog.Services;

public sealed class BlogRepository
{
    private static readonly Regex ContentPathPattern = new(
        @"^(?<year>\d{4})/(?<month>\d{1,2})/(?<day>\d{1,2})/(?<slug>[a-z0-9-]+)\.html$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex H1Regex = new(
        @"<h1\b[^>]*>(?<title>.*?)</h1>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled);

    private readonly HttpClient http;
    private IReadOnlyList<BlogPost>? cachedPosts;

    public BlogRepository(HttpClient http)
    {
        this.http = http;
    }

    public async Task<IReadOnlyList<BlogPost>> GetAllPostsAsync()
    {
        if (cachedPosts is not null)
            return cachedPosts;

        string manifest;
        try
        {
            manifest = await http.GetStringAsync("content/posts-manifest.txt");
        }
        catch (HttpRequestException)
        {
            cachedPosts = [];
            return cachedPosts;
        }

        var draftPosts = manifest
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Replace('\\', '/'))
            .Select(TryCreatePost)
            .Where(post => post is not null)
            .Cast<BlogPost>()
            .ToList();

        var posts = await Task.WhenAll(draftPosts.Select(async post =>
        {
            try
            {
                var html = await http.GetStringAsync(post.ContentPath);
                var titleFromH1 = ExtractTitleFromHtml(html);
                if (string.IsNullOrWhiteSpace(titleFromH1))
                    return post;

                return new BlogPost
                {
                    Title = titleFromH1,
                    Slug = post.Slug,
                    PublishedOn = post.PublishedOn,
                    ContentPath = post.ContentPath
                };
            }

            catch (HttpRequestException)
            {
                return post;
            }
        }));

        var orderedPosts = posts
            .OrderByDescending(post => post.PublishedOn)
            .ThenBy(post => post.Title)
            .ToList();

        cachedPosts = orderedPosts;
        return cachedPosts;
    }

    public async Task<BlogPost?> GetLatestPostAsync()
    {
        var posts = await GetAllPostsAsync();
        return posts.FirstOrDefault();
    }

    public async Task<BlogPost?> FindAsync(int year, int month, int day, string slug)
    {
        var posts = await GetAllPostsAsync();
        return posts.FirstOrDefault(post =>
            post.PublishedOn.Year == year &&
            post.PublishedOn.Month == month &&
            post.PublishedOn.Day == day &&
            string.Equals(post.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    private static BlogPost? TryCreatePost(string relativePath)
    {
        var match = ContentPathPattern.Match(relativePath);
        if (!match.Success)
            return null;

        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        var slug = match.Groups["slug"].Value.ToLowerInvariant();

        return new BlogPost
        {
            Title = SlugToTitle(slug),
            Slug = slug,
            PublishedOn = new DateOnly(year, month, day),
            ContentPath = $"content/{relativePath}"
        };
    }

    private static string SlugToTitle(string slug)
    {
        return string.Join(' ', slug
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string? ExtractTitleFromHtml(string html)
    {
        var match = H1Regex.Match(html);
        if (!match.Success)
            return null;

        var raw = match.Groups["title"].Value;
        var stripped = HtmlTagRegex.Replace(raw, string.Empty);
        var decoded = WebUtility.HtmlDecode(stripped).Trim();
        return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
    }
}

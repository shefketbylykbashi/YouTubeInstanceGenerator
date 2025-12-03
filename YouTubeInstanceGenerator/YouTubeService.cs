using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;

public class YouTubeService
{
    private readonly string _apiKey;
    private readonly HttpClient _http = new HttpClient();

    public YouTubeService(string apiKey)
    {
        _apiKey = apiKey;
    }

    public async Task<List<YouTubeVideo>> GetLiveStreamsAsync(int maxResults)
    {
        string url =
            $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&eventType=live&q=live&maxResults={maxResults}&key={_apiKey}";

        var json = await _http.GetStringAsync(url);
        var doc = JsonDocument.Parse(json);

        var results = new List<YouTubeVideo>();

        foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            var snippet = item.GetProperty("snippet");

            var title = snippet.GetProperty("title").GetString() ?? "";
            var channelTitle = snippet.GetProperty("channelTitle").GetString() ?? "";

            results.Add(new YouTubeVideo
            {
                VideoId = item.GetProperty("id").GetProperty("videoId").GetString(),
                Title = title,
                ChannelTitle = channelTitle,
                CategoryId = "unknown" // search API doesn't give categoryId directly
            });
        }

        return results;
    }

    public async Task<List<YouTubeVideo>> GetLiveStreamsAsync(
    char mode,
    DateTime? startDate,
    DateTime? endDate,
    int maxResults)
    {
        Console.WriteLine($"Mode: {mode}, Start: {startDate}, End: {endDate}");

        var results = new List<YouTubeVideo>();
        string eventType;
        string dateParams = "";

        switch (mode)
        {
            case 'n': // Live NOW → MUST NOT use dates
                eventType = "live";
                break;

            case 'p': // Past streams → MUST use dates
                if (startDate == null || endDate == null)
                    throw new ArgumentException("Past mode requires start and end dates!");

                eventType = "completed";
                dateParams =
                    $"&publishedAfter={startDate.Value.ToUniversalTime():o}" +
                    $"&publishedBefore={endDate.Value.ToUniversalTime():o}";
                break;

            case 'f': // Future → MUST use dates
                if (startDate == null || endDate == null)
                    throw new ArgumentException("Future mode requires start and end dates!");

                eventType = "upcoming";
                dateParams =
                    $"&publishedAfter={startDate.Value.ToUniversalTime():o}" +
                    $"&publishedBefore={endDate.Value.ToUniversalTime():o}";
                break;

            default:
                throw new ArgumentException("Mode must be 'p', 'n', or 'f'");
        }

        string qParam = mode == 'n' ? "&q=live" : ""; // Only needed for LIVE NOW

        string searchUrl =
            $"https://www.googleapis.com/youtube/v3/search" +
            $"?part=snippet&type=video&eventType={eventType}" +
            $"{qParam}" +
            $"{dateParams}" +
            $"&regionCode=US" +
            $"&maxResults={Math.Min(maxResults, 50)}" +
            $"&key={_apiKey}";

        Console.WriteLine("SEARCH URL:");
        Console.WriteLine(searchUrl);

        string json = await _http.GetStringAsync(searchUrl);
        Console.WriteLine("SEARCH RESPONSE:");
        Console.WriteLine(json);

        using var searchDoc = JsonDocument.Parse(json);

        var items = searchDoc.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .ToList();

        Console.WriteLine($"Found items: {items.Count}");

        if (!items.Any())
            return results;

        var ids = items
            .Select(i => i.GetProperty("id").GetProperty("videoId").GetString())
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        Console.WriteLine($"Unique IDs: {ids.Count}");

        if (!ids.Any())
            return results;

        string videosUrl =
            $"https://www.googleapis.com/youtube/v3/videos" +
            $"?part=snippet,liveStreamingDetails" +
            $"&id={string.Join(",", ids)}" +
            $"&key={_apiKey}";

        Console.WriteLine("VIDEOS URL:");
        Console.WriteLine(videosUrl);

        var videosJson = await _http.GetStringAsync(videosUrl);
        Console.WriteLine("VIDEOS RESPONSE:");
        Console.WriteLine(videosJson);

        using var videosDoc = JsonDocument.Parse(videosJson);

        foreach (var video in videosDoc.RootElement.GetProperty("items").EnumerateArray())
        {
            var snippet = video.GetProperty("snippet");

            // Titles are never null, fail-safe backup
            string title = snippet.GetProperty("title").GetString() ?? "Untitled";
            string channel = snippet.GetProperty("channelTitle").GetString() ?? "Unknown Channel";

            results.Add(new YouTubeVideo
            {
                VideoId = video.GetProperty("id").GetString(),
                Title = title,
                ChannelTitle = channel,
                CategoryId = "unknown"
            });

            if (results.Count >= maxResults)
                break;
        }

        Console.WriteLine($"FINAL COUNT: {results.Count}");
        return results;
    }


    public static string MapCategoryToGenre(string categoryId, string title)
    {
        // If someday you fetch real categoryId from /videos API, you can still keep this:
        switch (categoryId)
        {
            case "25": return "news";
            case "17": return "sports";
            case "10": return "music";
            case "1": return "movie";
            case "24": return "drama";
            case "22": return "talk";
            case "20": return "kids";
            case "27": return "documentary";
        }

        // Fallback: infer genre from title keywords
        var t = title.ToLowerInvariant();

        if (t.Contains("news") || t.Contains("breaking"))
            return "news";
        if (t.Contains("football") || t.Contains("soccer") || t.Contains("nba") || t.Contains("live match") || t.Contains("sports"))
            return "sports";
        if (t.Contains("music") || t.Contains("dj") || t.Contains("song") || t.Contains("remix"))
            return "music";
        if (t.Contains("podcast") || t.Contains("talk") || t.Contains("interview"))
            return "talk";
        if (t.Contains("kids") || t.Contains("cartoon") || t.Contains("children"))
            return "kids";
        if (t.Contains("documentary") || t.Contains("history"))
            return "documentary";
        if (t.Contains("game") || t.Contains("gaming") || t.Contains("fortnite") || t.Contains("minecraft") || t.Contains("pubg"))
            return "gaming";
        if (t.Contains("tech") || t.Contains("review") || t.Contains("unboxing"))
            return "tech";

        return "variety"; // generic fallback
    }
}

public class YouTubeVideo
{
    public string VideoId { get; set; }
    public string Title { get; set; }
    public string ChannelTitle { get; set; }
    public string CategoryId { get; set; }
}

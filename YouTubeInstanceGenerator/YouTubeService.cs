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

    public async Task<List<YouTubeVideo>> GetPublicStreamsAsync(
    char mode,
    int maxResults,
    DateTime? start = null,
    DateTime? end = null)
    {
        string eventType = mode switch
        {
            'n' => "live",
            'f' => "upcoming",
            'p' => null, // Past streams handled later
            _ => throw new ArgumentException("Mode must be 'n', 'f', or 'p'")
        };

        string dateParams = "";
        if (mode == 'p' && start.HasValue && end.HasValue)
        {
            dateParams =
                $"&publishedAfter={start.Value.ToUniversalTime():o}" +
                $"&publishedBefore={end.Value.ToUniversalTime():o}";
        }

        string eventParam = eventType != null ? $"&eventType={eventType}" : "";

        string url =
            $"https://www.googleapis.com/youtube/v3/search" +
            $"?part=snippet&type=video&q=live" +
            $"{eventParam}" +
            $"{dateParams}" +
            $"&maxResults={maxResults}" +
            $"&key={_apiKey}";

        Console.WriteLine("SEARCH URL => " + url);

        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);

        var ids = doc.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(i => i.GetProperty("id").GetProperty("videoId").GetString())
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        if (!ids.Any())
            return new List<YouTubeVideo>();

        string videosUrl =
            $"https://www.googleapis.com/youtube/v3/videos" +
            $"?part=snippet,liveStreamingDetails" +
            $"&id={string.Join(",", ids)}" +
            $"&key={_apiKey}";

        Console.WriteLine("VIDEOS URL => " + videosUrl);

        var videosJson = await _http.GetStringAsync(videosUrl);
        using var videosDoc = JsonDocument.Parse(videosJson);

        var results = new List<YouTubeVideo>();

        foreach (var v in videosDoc.RootElement.GetProperty("items").EnumerateArray())
        {
            var snippet = v.GetProperty("snippet");
            var details = v.GetProperty("liveStreamingDetails");
            DateTime? dt(string name) =>
                details.TryGetProperty(name, out var x) ?
                DateTime.Parse(x.GetString()) : null;

            results.Add(new YouTubeVideo
            {
                VideoId = v.GetProperty("id").GetString(),
                Title = snippet.GetProperty("title").GetString(),
                ChannelTitle = snippet.GetProperty("channelTitle").GetString(),
                CategoryId = snippet.TryGetProperty("categoryId", out var c) ? c.GetString() : "unknown",
                ScheduledStart = dt("scheduledStartTime"),
                ActualStart = dt("actualStartTime"),
                ActualEnd = dt("actualEndTime")
            });
        }

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
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
}

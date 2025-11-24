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

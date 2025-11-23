using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

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

            results.Add(new YouTubeVideo
            {
                VideoId = item.GetProperty("id").GetProperty("videoId").GetString(),
                Title = snippet.GetProperty("title").GetString(),
                ChannelTitle = snippet.GetProperty("channelTitle").GetString(),
                CategoryId = "unknown"
            });
        }

        return results;
    }

    // Genre Mapping Option A
    public static string MapCategoryToGenre(string categoryId)
    {
        return categoryId switch
        {
            "25" => "news",
            "17" => "sports",
            "10" => "music",
            "1" => "movie",
            "24" => "drama",
            "22" => "talk",
            "20" => "kids",
            "27" => "documentary",
            _ => "drama"
        };
    }
}

public class YouTubeVideo
{
    public string VideoId { get; set; }
    public string Title { get; set; }
    public string ChannelTitle { get; set; }
    public string CategoryId { get; set; }
}

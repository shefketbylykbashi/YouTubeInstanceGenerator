using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using YouTubeInstanceGenerator.Models;

class Program
{
    static async Task Main(string[] args)
    {
        //var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("C:\\Users\\shefk\\source\\repos\\YouTubeInstanceGenerator\\YouTubeInstanceGenerator\\appsettings.json"));
        string apiKey = "AIzaSyDtv2jbYTPrFD_gg55kUm0sHVannpb8yEc";
        int maxStreams = 20;

        var yt = new YouTubeService(apiKey);
        var streams = await yt.GetLiveStreamsAsync(maxStreams);

        Console.WriteLine($"Fetched {streams.Count} live streams!");

        var now = DateTime.Now;
        int startMinute = now.Hour * 60 + now.Minute;

        var instance = new Instance
        {
            opening_time = startMinute,
            closing_time = startMinute + 600,
            min_duration = 30,
            max_consecutive_genre = 2,
            channels_count = streams.Count,
            switch_penalty = 3,
            termination_penalty = 15,
            priority_blocks = new List<PriorityBlock>
            {
                new PriorityBlock
                {
                    start = startMinute + 120,
                    end = startMinute + 180,
                    allowed_channels = new List<int> { 0, 1, 2 }
                }
            },
            time_preferences = new List<TimePreference>
            {
                new TimePreference
                {
                    start = startMinute,
                    end = startMinute + 120,
                    preferred_genre = "news",
                    bonus = 30
                }
            },
            channels = new List<Channel>()
        };

        int channelId = 0;

        foreach (var s in streams)
        {
            instance.channels.Add(new Channel
            {
                channel_id = channelId,
                channel_name = s.ChannelTitle,
                programs = new List<ProgramItem>
                {
                    new ProgramItem
                    {
                        program_id = $"{s.VideoId}",
                        start = startMinute,
                        end = startMinute + 60,
                        genre = YouTubeService.MapCategoryToGenre(s.CategoryId),
                        score = new Random().Next(40, 100)
                    }
                }
            });

            channelId++;
        }

        Directory.CreateDirectory("Output");
        File.WriteAllText("Output/instance.json",
            JsonSerializer.Serialize(instance, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine("Instance saved to Output/instance.json");

        var lines = new List<string>();
        lines.Add("channel_id,program_id,url");

        foreach (var ch in instance.channels)
        {
            foreach (var p in ch.programs)
            {
                string url = $"https://www.youtube.com/watch?v={p.program_id}";
                lines.Add($"{ch.channel_id},{p.program_id},{url}");
            }
        }

        File.WriteAllLines("Output/livestream_urls.csv", lines);

        Console.WriteLine("Livestream URL list saved to Output/livestream_urls.csv");
    }
}

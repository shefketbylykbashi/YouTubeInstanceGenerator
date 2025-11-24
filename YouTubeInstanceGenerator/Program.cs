using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using YouTubeInstanceGenerator.Models;
using YouTubeInstanceGenerator;

class Program
{
    static async Task Main(string[] args)
    {
        // >>> CONFIG <<<
        string apiKey = "AIzaSyDtv2jbYTPrFD_gg55kUm0sHVannpb8yEc";   // TODO: replace
        int maxStreams = 80;                   // fetch more streams → more potential channels
        int maxChannelsToUse = 40;             // cap channels used in instance
        bool includeLink = true;               // control whether link is included in JSON

        var yt = new YouTubeService(apiKey);
        var streams = await yt.GetLiveStreamsAsync(maxStreams);

        if (streams.Count == 0)
        {
            Console.WriteLine("No live streams fetched.");
            return;
        }

        // Shuffle and take subset for channels
        var rand = new Random();
        var selectedStreams = streams
            .OrderBy(_ => Guid.NewGuid())
            .Take(maxChannelsToUse)
            .ToList();

        int channelCount = selectedStreams.Count;

        // >>> REALISTIC TV DAY: 07:00–23:00 <<<
        int opening = 7 * 60;   // 07:00 → 420
        int closing = 23 * 60;  // 23:00 → 1380
        int minDuration = 30;   // like your example
        int maxConsecutiveGenre = 2;
        int switchPenalty = 3;
        int terminationPenalty = 15;

        // >>> BUILD INSTANCE <<<
        var instance = new Instance
        {
            opening_time = opening,
            closing_time = closing,
            min_duration = minDuration,
            max_consecutive_genre = maxConsecutiveGenre,
            channels_count = channelCount,
            switch_penalty = switchPenalty,
            termination_penalty = terminationPenalty,
            priority_blocks = GeneratePriorityBlocks(opening, closing, channelCount, rand),
            time_preferences = GenerateStructuredPreferences(opening, closing),
            channels = new List<Channel>()
        };

        // >>> BUILD CHANNELS WITH CLEAN, CONTINUOUS SCHEDULES <<<
        int channelId = 0;
        foreach (var s in selectedStreams)
        {
            string baseGenre = YouTubeService.MapCategoryToGenre(s.CategoryId, s.Title);

            var channel = new Channel
            {
                channel_id = channelId,
                channel_name = s.ChannelTitle,
                programs = new List<ProgramItem>()
            };

            // Start exactly at opening_time like RTK1 / KTV examples
            int currentTime = opening;
            int programIndex = 0;

            // We keep scheduling until we reach the closing time
            while (currentTime < closing)
            {
                int remaining = closing - currentTime;

                if (remaining < minDuration)
                    break;

                // Prefer medium lengths (triangular) but respect remaining time
                int maxAllowed = Math.Min(180, remaining);
                int duration = Triangular(rand, minDuration, maxAllowed);
                int start = currentTime;
                int end = start + duration;

                // If we are very close to closing, just fill to the end
                if (closing - end < minDuration)
                {
                    end = closing;
                    duration = end - start;
                }

                string programGenre = PickProgramGenre(rand, baseGenre);
                int score = rand.Next(30, 101); // 30–100 more realistic TV scores

                channel.programs.Add(new ProgramItem
                {
                    program_id = $"{s.VideoId}_{programIndex}",
                    start = start,
                    end = end,
                    genre = programGenre,
                    score = score,
                    link = includeLink ? $"https://www.youtube.com/watch?v={s.VideoId}" : null
                });

                programIndex++;
                currentTime = end; // continuous: next program starts when previous ends
            }

            // Only add channels that ended up with at least one program
            if (channel.programs.Any())
                instance.channels.Add(channel);

            channelId++;
        }

        // Update channels_count to actual number created
        instance.channels_count = instance.channels.Count;

        // >>> OUTPUT JSON <<<
        Directory.CreateDirectory("Output");

        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText("Output/instance.json",
            JsonSerializer.Serialize(instance, opts));

        Console.WriteLine("Saved: Output/instance.json");

        // >>> OUTPUT CSV (for debugging / inspection) <<<
        WriteCSV(instance);
    }

    // -----------------------------------------------
    // PRIORITY BLOCKS – realistic morning & evening focus
    // -----------------------------------------------
    static List<PriorityBlock> GeneratePriorityBlocks(int opening, int closing, int channels, Random rand)
    {
        var blocks = new List<PriorityBlock>();

        // Example-style:
        //  - Morning block near opening (e.g. 07:00–09:00)
        //  - Prime-time block near closing (e.g. last 2–3 hours)

        int daySpan = closing - opening;

        // Morning block
        int morningStart = opening + 60; // 1h after opening
        int morningEnd = Math.Min(closing, morningStart + 90); // 1.5h
        var morningAllowed = SampleChannelSubset(channels, rand, min: 3, max: Math.Max(3, channels / 4));

        blocks.Add(new PriorityBlock
        {
            start = morningStart,
            end = morningEnd,
            allowed_channels = morningAllowed
        });

        // Prime-time block in the last quarter of the day
        int primeStart = opening + (int)(daySpan * 0.75);
        int primeEnd = closing; // last hours until closing
        var primeAllowed = SampleChannelSubset(channels, rand, min: 4, max: Math.Max(4, channels / 3));

        blocks.Add(new PriorityBlock
        {
            start = primeStart,
            end = primeEnd,
            allowed_channels = primeAllowed
        });

        return blocks;
    }

    static List<int> SampleChannelSubset(int channels, Random rand, int min, int max)
    {
        int count = rand.Next(min, Math.Max(min + 1, max + 1));
        count = Math.Min(count, channels);

        var indices = Enumerable.Range(0, channels).ToList();
        indices = indices.OrderBy(_ => rand.Next()).Take(count).ToList();
        return indices;
    }

    // -----------------------------------------------
    // TIME PREFERENCES – structured like your example
    // -----------------------------------------------
    static List<TimePreference> GenerateStructuredPreferences(int opening, int closing)
    {
        var prefs = new List<TimePreference>();

        int daySpan = closing - opening;
        // Roughly split day into 5 blocks like your example
        int block = daySpan / 5;

        // Block 1: early morning – news
        prefs.Add(new TimePreference
        {
            start = opening,
            end = opening + block,
            preferred_genre = "news",
            bonus = 30
        });

        // Block 2: morning – talk / kids
        prefs.Add(new TimePreference
        {
            start = opening + block,
            end = opening + 2 * block,
            preferred_genre = "talk",
            bonus = 20
        });

        // Block 3: midday – drama
        prefs.Add(new TimePreference
        {
            start = opening + 2 * block,
            end = opening + 3 * block,
            preferred_genre = "drama",
            bonus = 25
        });

        // Block 4: afternoon / evening – movie
        prefs.Add(new TimePreference
        {
            start = opening + 3 * block,
            end = opening + 4 * block,
            preferred_genre = "movie",
            bonus = 40
        });

        // Block 5: late evening – music
        prefs.Add(new TimePreference
        {
            start = opening + 4 * block,
            end = closing,
            preferred_genre = "music",
            bonus = 15
        });

        return prefs;
    }

    // -----------------------------------------------
    // GENRE SELECTION PER PROGRAM
    // -----------------------------------------------
    static string PickProgramGenre(Random rand, string baseGenre)
    {
        // All possible genres
        string[] allGenres =
        {
            "news", "sports", "music", "gaming", "talk",
            "movie", "documentary", "kids", "tech", "variety", "drama"
        };

        // 70% → keep base genre
        if (!string.IsNullOrEmpty(baseGenre) && rand.NextDouble() < 0.7)
            return baseGenre;

        // 30% → small variation (but bias towards similar "family" where possible)
        // For simplicity: pick random from allGenres
        return allGenres[rand.Next(allGenres.Length)];
    }

    // Triangular distribution: more realistic durations
    static int Triangular(Random r, int min, int max)
    {
        if (min >= max) return min;
        double u = r.NextDouble();
        return (int)(min + (max - min) * Math.Sqrt(u));
    }

    // -----------------------------------------------
    // CSV EXPORT FOR MANUAL INSPECTION
    // -----------------------------------------------
    static void WriteCSV(Instance instance)
    {
        var lines = new List<string> { "channel_id,name,program_id,start,end,genre,score,url" };

        foreach (var ch in instance.channels)
        {
            foreach (var p in ch.programs)
            {
                lines.Add(
                    $"{ch.channel_id}," +
                    $"\"{ch.channel_name}\"," +
                    $"{p.program_id}," +
                    $"{p.start},{p.end}," +
                    $"{p.genre},{p.score}," +
                    $"{p.link}"
                );
            }
        }

        Directory.CreateDirectory("Output");
        File.WriteAllLines("Output/livestream_urls.csv", lines);
        Console.WriteLine("Saved: Output/livestream_urls.csv");
    }
}

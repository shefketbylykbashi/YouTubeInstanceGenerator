using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using YouTubeInstanceGenerator.Models;
using YouTubeInstanceGenerator;
using System.Globalization;

class Program
{
    static async Task Main(string[] args)
    {
        // Defaults
        string apiKey = null;
        int maxChannelsToUse = 40;
        bool includeLink = true;

        string mode = "n"; // n = now, f = future, p = past
        int startOffset = 0;
        int endOffset = 1;

        // ---------------- Command Line Parser ----------------
        foreach (var arg in args)
        {
            var parts = arg.Split('=');
            if (parts.Length != 2) continue;

            string key = parts[0].ToLower();
            string value = parts[1];

            switch (key)
            {
                case "--apikey":
                    apiKey = value;
                    break;
                case "--maxchannels":
                    int.TryParse(value, out maxChannelsToUse);
                    break;
                case "--includelink":
                    bool.TryParse(value, out includeLink);
                    break;
                case "--mode":
                    mode = value.ToLower(); // n / f / p
                    break;
                case "--start":
                    int.TryParse(value, out startOffset);
                    break;
                case "--end":
                    int.TryParse(value, out endOffset);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("❌ Missing required parameter: --apiKey");
            return;
        }

        Directory.CreateDirectory("Output");
        var yt = new YouTubeService(apiKey);

        Console.WriteLine($"STARTING MODE: {mode.ToUpper()}");
        Console.WriteLine($"Start Offset: {startOffset} Day(s)");
        Console.WriteLine($"End Offset:   {endOffset} Day(s)");

        Instance instance = null;
        string filename = "";

        // ---------------- FETCH DATA BASED ON MODE ----------------
        if (mode == "n")   // LIVE NOW
        {
            var live = await yt.GetPublicStreamsAsync('n', 100);
            Console.WriteLine($"LIVE COUNT: {live.Count}");

            instance = BuildLiveInstance(live, maxChannelsToUse, includeLink);
            filename = "instance_live.json";
            WriteCSV(instance);
        }
        else if (mode == "f")   // UPCOMING
        {
            var upcoming = await yt.GetPublicStreamsAsync(
                'f', maxChannelsToUse,
                DateTime.UtcNow.AddDays(startOffset),
                DateTime.UtcNow.AddDays(endOffset));

            Console.WriteLine($"UPCOMING COUNT: {upcoming.Count}");

            instance = BuildSingleProgramInstance(upcoming, includeLink, "Upcoming");
            filename = "instance_upcoming.json";
        }
        else if (mode == "p") // PAST
        {
            var past = await yt.GetPublicStreamsAsync(
                'p', 100,
                DateTime.UtcNow.AddDays(startOffset),
                DateTime.UtcNow.AddDays(endOffset));

            Console.WriteLine($"PAST COUNT: {past.Count}");

            instance = BuildSingleProgramInstance(past, includeLink, "Past", true);
            filename = "instance_past.json";
        }
        else
        {
            Console.WriteLine("❌ Invalid mode! Use: --mode=n|f|p");
            return;
        }

        // Save JSON
        instance.channels_count = instance.channels.Count;
        SaveInstance(filename, instance);

        Console.WriteLine("\n✔ DONE — Exported instance: " + filename);
    }
    static int ToMinutes(DateTime dt) => dt.ToLocalTime().Hour * 60 + dt.ToLocalTime().Minute;
    static Instance BuildLiveInstance(List<YouTubeVideo> streams, int maxChannels, bool includeLink)
    {
        var rand = new Random();

        // Live = 0 → end dynamically based on program lengths
        int opening = 0;
        int closing = 0;

        var instance = new Instance
        {
            opening_time = opening,
            closing_time = closing,
            min_duration = 30,
            max_consecutive_genre = 2,
            switch_penalty = 3,
            termination_penalty = 15,
            channels = new()
        };

        if (streams.Count == 0) return instance;
        var shuffled = streams.OrderBy(_ => rand.Next()).ToList();

        for (int channelId = 0; channelId < maxChannels && shuffled.Count > 0; channelId++)
        {
            var ch = new Channel
            {
                channel_id = channelId,
                channel_name = $"LIVE Stream {channelId + 1}",
                programs = new()
            };

            int t = opening;
            int idx = 0;

            while (idx < 5) // Max ~5 segments per live stream
            {
                var video = shuffled[rand.Next(shuffled.Count)];

                string genre = YouTubeService.MapCategoryToGenre(video.CategoryId, video.Title);
                int dur = Triangular(rand, 20, 160);

                int start = t;
                int end = start + dur;

                ch.programs.Add(new ProgramItem
                {
                    program_id = $"L_CH{channelId}_P{idx}",
                    start = start,
                    end = end,
                    genre = genre,
                    score = rand.Next(40, 100),
                    link = includeLink ? $"https://www.youtube.com/watch?v={video.VideoId}" : null
                });

                t = end;
                idx++;
            }

            closing = Math.Max(closing, t);
            instance.channels.Add(ch);
        }

        // Update closing time dynamically
        instance.closing_time = closing;
        instance.channels_count = instance.channels.Count;

        // Dynamic time prefs + priority blocks
        instance.time_preferences = GenerateStructuredPreferences(opening, closing);
        instance.priority_blocks = GeneratePriorityBlocks(opening, closing, maxChannels, rand);

        return instance;
    }

    static Instance BuildSingleProgramInstance(List<YouTubeVideo> items, bool includeLink, string label, bool requireEndTime = false)
    {
        var rand = new Random();

        int opening = 0;
        int closing = 24 * 60;

        var instance = new Instance
        {
            opening_time = opening,
            closing_time = closing,
            min_duration = 30,
            max_consecutive_genre = 2,
            switch_penalty = 3,
            termination_penalty = 15,
            channels = new()
        };

        foreach (var item in items)
        {
            if (requireEndTime && (item.ScheduledStart == null || item.ActualEnd == null))
                continue;

            DateTime start = item.ScheduledStart ?? DateTime.UtcNow;
            DateTime end = start.AddMinutes(rand.Next(45, 180)); // more realistic

            int S = ToMinutes(start);
            int E = ToMinutes(end);

            if (S < opening) S = opening;
            if (E > closing) E = closing;

            instance.channels.Add(new Channel
            {
                channel_id = instance.channels.Count,
                channel_name = $"{item.ChannelTitle} ({label})",
                programs = new()
            {
                new ProgramItem
                {
                    program_id = $"{label[..2]}_{item.VideoId}",
                    start = S,
                    end = E,
                    genre = YouTubeService.MapCategoryToGenre(item.CategoryId, item.Title),
                    score = rand.Next(50, 90),
                    link = includeLink ? $"https://www.youtube.com/watch?v={item.VideoId}" : null
                }
            }
            });
        }

        instance.channels_count = instance.channels.Count;

        // ✔️ Dynamic preference + priority for UPCOMING
        instance.time_preferences = GenerateStructuredPreferences(opening, closing);
        instance.priority_blocks = GeneratePriorityBlocks(opening, closing, instance.channels_count, rand);

        return instance;
    }

    static void SaveInstance(string filename, Instance instance)
    {
        File.WriteAllText($"Output/{filename}",
            JsonSerializer.Serialize(instance, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Saved → Output/{filename} ({instance.channels_count} channels)");
    }
    // -----------------------------------------------
    // PRIORITY BLOCKS – realistic morning & evening focus
    // -----------------------------------------------
    static List<PriorityBlock> GeneratePriorityBlocks(int opening, int closing, int channels, Random rand)
    {
        var blocks = new List<PriorityBlock>();

        int H(int hour) => hour * 60;

        // Helper to shuffle and pick channel subsets
        List<int> PickTopChannels(int minPercent)
        {
            int count = Math.Max(2, (channels * minPercent) / 100);
            return Enumerable.Range(0, channels)
                .OrderBy(_ => rand.Next())
                .Take(count)
                .ToList();
        }

        // SMALL SHIFT for dynamic realism
        int shift() => rand.Next(-20, 21);

        // 1️⃣ MORNING PRIORITY — News focus
        int morningStart = H(7) + shift();
        int morningEnd = H(9) + shift();
        blocks.Add(new PriorityBlock
        {
            start = Math.Max(opening, morningStart),
            end = Math.Min(closing, morningEnd),
            allowed_channels = PickTopChannels(35) // Top 35% channels
        });

        // 2️⃣ AFTERNOON PRIORITY — Kids / entertainment
        int afternoonStart = H(15) + shift();
        int afternoonEnd = H(17) + shift();
        blocks.Add(new PriorityBlock
        {
            start = Math.Max(opening, afternoonStart),
            end = Math.Min(closing, afternoonEnd),
            allowed_channels = PickTopChannels(45) // Top 45%
        });

        // 3️⃣ PRIME TIME — Highest priority 🏆
        int primeStart = H(19) + shift();
        int primeEnd = H(22) + shift();
        blocks.Add(new PriorityBlock
        {
            start = Math.Max(opening, primeStart),
            end = Math.Min(closing, primeEnd),
            allowed_channels = PickTopChannels(60) // Top 60% channels allowed
        });

        // Sort blocks by starting time to avoid weird overlaps
        return blocks.OrderBy(b => b.start).ToList();
    }

    // -----------------------------------------------
    // TIME PREFERENCES – structured like your example
    // -----------------------------------------------
    static List<TimePreference> GenerateStructuredPreferences(int opening, int closing)
    {
        var prefs = new List<TimePreference>();
        var rand = new Random();

        // Dynamic shift helper (smaller shift to avoid chaos)
        int shift() => rand.Next(-20, 21); // +/- up to 20 min

        // Genre pools by time of day
        string Pick(params string[] arr) => arr[rand.Next(arr.Length)];

        int current = opening;

        // Block 1: Morning – News/Talk
        {
            int end = Math.Min(closing, (9 * 60) + shift());
            prefs.Add(new TimePreference
            {
                start = current,
                end = end,
                preferred_genre = Pick("news", "talk"),
                bonus = rand.Next(40, 80)
            });
            current = end;
        }

        // Block 2: Daytime – Talk/Kids/Documentary
        {
            int end = Math.Min(closing, (12 * 60) + shift());
            prefs.Add(new TimePreference
            {
                start = current,
                end = end,
                preferred_genre = Pick("talk", "kids", "documentary"),
                bonus = rand.Next(20, 45)
            });
            current = end;
        }

        // Block 3: Midday – Drama/Documentary
        {
            int end = Math.Min(closing, (15 * 60) + shift());
            prefs.Add(new TimePreference
            {
                start = current,
                end = end,
                preferred_genre = Pick("drama", "documentary"),
                bonus = rand.Next(25, 50)
            });
            current = end;
        }

        // Block 4: Afternoon – Kids/Gaming
        {
            int end = Math.Min(closing, (18 * 60) + shift());
            prefs.Add(new TimePreference
            {
                start = current,
                end = end,
                preferred_genre = Pick("kids", "gaming"),
                bonus = rand.Next(30, 55)
            });
            current = end;
        }

        // Block 5: Prime Time – Sports/Movie/Drama
        {
            int end = Math.Min(closing, (21 * 60) + shift());
            prefs.Add(new TimePreference
            {
                start = current,
                end = end,
                preferred_genre = Pick("sports", "movie", "drama"),
                bonus = rand.Next(70, 120)
            });
            current = end;
        }

        // Block 6: Evening – Music/Talk/Gaming/Variety
        {
            prefs.Add(new TimePreference
            {
                start = current,
                end = closing,
                preferred_genre = Pick("music", "talk", "gaming", "variety"),
                bonus = rand.Next(25, 60)
            });
        }

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
            foreach (var p in ch.programs)
                lines.Add(
                    $"{ch.channel_id}," +
                    $"\"{ch.channel_name}\"," +
                    $"{p.program_id}," +
                    $"{p.start},{p.end}," +
                    $"{p.genre},{p.score}," +
                    $"{p.link}");

        Directory.CreateDirectory("Output");
        File.WriteAllLines("Output/livestream_urls.csv", lines);
        Console.WriteLine("Saved: Output/livestream_urls.csv");
    }
}

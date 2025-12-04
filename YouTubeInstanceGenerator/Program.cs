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
        string apiKey = "AIzaSyDtv2jbYTPrFD_gg55kUm0sHVannpb8yEc";
        int maxStreams = 1000;
        int maxChannelsToUse = 40;
        bool includeLink = true;

        var yt = new YouTubeService(apiKey);

        var live = await yt.GetPublicStreamsAsync('n', 40);
        Console.WriteLine($"LIVE NOW: {live.Count}");

        var upcoming = await yt.GetPublicStreamsAsync('f', 20);
        Console.WriteLine($"UPCOMING: {upcoming.Count}");

        //var past = await yt.GetPublicStreamsAsync('p', 20,
        //    DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        //Console.WriteLine($"PAST: {past.Count}");

        if (live.Count == 0) live = new List<YouTubeVideo>();

        // Shuffle for live channel distribution
        var rand = new Random();
        var shuffled = live.OrderBy(_ => rand.Next()).ToList();

        int opening = 7 * 60;
        int closing = 23 * 60;
        int minDuration = 30;

        var instance = new Instance
        {
            opening_time = opening,
            closing_time = closing,
            min_duration = minDuration,
            max_consecutive_genre = 2,
            switch_penalty = 3,
            termination_penalty = 15,
            priority_blocks = GeneratePriorityBlocks(opening, closing, maxChannelsToUse, rand),
            time_preferences = GenerateStructuredPreferences(opening, closing),
            channels = new List<Channel>()
        };

        // -----------------------------
        // LIVE CHANNELS (original style)
        // -----------------------------
        int channelId = 0;

        for (int c = 0; c < maxChannelsToUse; c++)
        {
            if (!shuffled.Any()) break;

            var channel = new Channel
            {
                channel_id = channelId,
                channel_name = $"Live Channel {channelId + 1}",
                programs = new List<ProgramItem>()
            };

            string baseGenre = YouTubeService.MapCategoryToGenre(
                shuffled[0].CategoryId, shuffled[0].Title);

            int currentTime = opening;
            int i = 0;

            while (currentTime < closing)
            {
                var s = shuffled[rand.Next(shuffled.Count)];

                int remaining = closing - currentTime;
                if (remaining < minDuration) break;

                int duration = Triangular(rand, minDuration, Math.Min(200, remaining));
                int start = currentTime;
                int end = start + duration;

                if (closing - end < minDuration)
                {
                    end = closing;
                    duration = end - start;
                }

                channel.programs.Add(new ProgramItem
                {
                    program_id = $"CH{channelId}_L{i}",
                    start = start,
                    end = end,
                    genre = PickProgramGenre(rand, baseGenre),
                    score = rand.Next(40, 101),
                    link = includeLink ? $"https://www.youtube.com/watch?v={s.VideoId}" : null
                });

                currentTime = end;
                i++;
            }

            instance.channels.Add(channel);
            channelId++;
        }

        // ---------------------------------
        // UPCOMING → 1 STREAM = 1 CHANNEL
        // ---------------------------------
        foreach (var up in upcoming)
        {
            var start = up.ScheduledStart ?? DateTime.UtcNow.AddMinutes(10);
            var end = start.AddHours(2); // fallback when end time unknown

            instance.channels.Add(new Channel
            {
                channel_id = instance.channels.Count,
                channel_name = $"{up.ChannelTitle} (Upcoming)",
                programs = new List<ProgramItem>
                {
                    new ProgramItem
                    {
                        program_id = $"UP_{up.VideoId}",
                        start = ToMinutes(start),
                        end = ToMinutes(end),
                        genre = YouTubeService.MapCategoryToGenre(up.CategoryId, up.Title),
                        score = 75,
                        link = includeLink ? $"https://www.youtube.com/watch?v={up.VideoId}" : null
                    }
                }
            });
        }

        // ---------------------------------
        // PAST → 1 STREAM = 1 CHANNEL
        // ---------------------------------
        //foreach (var pa in past)
        //{
        //    if (pa.ActualStart == null || pa.ActualEnd == null)
        //        continue;

        //    instance.channels.Add(new Channel
        //    {
        //        channel_id = instance.channels.Count,
        //        channel_name = $"{pa.ChannelTitle} (Past)",
        //        programs = new List<ProgramItem>
        //        {
        //            new ProgramItem
        //            {
        //                program_id = $"PA_{pa.VideoId}",
        //                start = ToMinutes(pa.ActualStart.Value),
        //                end = ToMinutes(pa.ActualEnd.Value),
        //                genre = YouTubeService.MapCategoryToGenre(pa.CategoryId, pa.Title),
        //                score = 50,
        //                link = includeLink ? $"https://www.youtube.com/watch?v={pa.VideoId}" : null
        //            }
        //        }
        //    });
        //}

        instance.channels_count = instance.channels.Count;

        Directory.CreateDirectory("Output");
        File.WriteAllText("Output/instance.json",
            JsonSerializer.Serialize(instance, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine("Saved: Output/instance.json");

        WriteCSV(instance);
        static int ToMinutes(DateTime dt) =>
        dt.ToLocalTime().Hour * 60 + dt.ToLocalTime().Minute;

        static int Triangular(Random r, int min, int max)
        {
            if (min >= max) return min;
            double u = r.NextDouble();
            return (int)(min + (max - min) * Math.Sqrt(u));
        }

        static string PickProgramGenre(Random rand, string baseGenre)
        {
            string[] allGenres =
            { "news", "sports", "music", "gaming", "talk",
          "movie", "documentary", "kids", "tech",
          "variety", "drama" };

            if (!string.IsNullOrEmpty(baseGenre) && rand.NextDouble() < 0.7)
                return baseGenre;
            return allGenres[rand.Next(allGenres.Length)];
        }
    }
    static int ToMinutes(DateTime dt) => dt.ToLocalTime().Hour * 60 + dt.ToLocalTime().Minute;
    static Instance BuildLiveInstance(List<YouTubeVideo> streams, int maxChannels, bool includeLink)
    {
        int opening = 7 * 60;
        int closing = 23 * 60;
        var rand = new Random();

        var instance = new Instance
        {
            opening_time = opening,
            closing_time = closing,
            min_duration = 30,
            max_consecutive_genre = 2,
            switch_penalty = 3,
            termination_penalty = 15,
            priority_blocks = GeneratePriorityBlocks(opening, closing, maxChannels, rand),
            time_preferences = GenerateStructuredPreferences(opening, closing),
            channels = new List<Channel>()
        };

        if (streams.Count == 0) return instance;

        var shuffled = streams.OrderBy(_ => rand.Next()).ToList();

        for (int channelId = 0; channelId < maxChannels && shuffled.Count > 0; channelId++)
        {
            var ch = new Channel
            {
                channel_id = channelId,
                channel_name = $"Live Channel {channelId + 1}",
                programs = new List<ProgramItem>()
            };

            int t = opening;
            int idx = 0;

            while (t < closing)
            {
                var prog = shuffled[rand.Next(shuffled.Count)];
                string genre = YouTubeService.MapCategoryToGenre(prog.CategoryId, prog.Title);

                int remain = closing - t;
                if (remain < 30) break;

                int dur = Triangular(rand, 30, Math.Min(200, remain));
                int start = t;
                int end = start + dur;
                if (closing - end < 30) end = closing;

                ch.programs.Add(new ProgramItem
                {
                    program_id = $"L_CH{channelId}_P{idx}",
                    start = start,
                    end = end,
                    genre = genre,
                    score = rand.Next(40, 100),
                    link = includeLink ? $"https://www.youtube.com/watch?v={prog.VideoId}" : null
                });

                t = end;
                idx++;
            }

            instance.channels.Add(ch);
        }

        instance.channels_count = instance.channels.Count;
        return instance;
    }

    static Instance BuildSingleProgramInstance(List<YouTubeVideo> items, bool includeLink, string label, bool requireEndTime = false)
    {
        var instance = new Instance
        {
            opening_time = 0,
            closing_time = 1440,
            min_duration = 1,
            max_consecutive_genre = 99,
            switch_penalty = 0,
            termination_penalty = 0,
            priority_blocks = new(),
            time_preferences = new(),
            channels = new()
        };

        foreach (var item in items)
        {
            if (requireEndTime &&
                (item.ActualStart == null || item.ActualEnd == null))
                continue;

            DateTime start = item.ScheduledStart ?? item.ActualStart ?? DateTime.UtcNow;
            DateTime end = item.ActualEnd ?? start.AddHours(2);

            instance.channels.Add(new Channel
            {
                channel_id = instance.channels.Count,
                channel_name = $"{item.ChannelTitle} ({label})",
                programs = new List<ProgramItem>
            {
                new ProgramItem
                {
                    program_id = $"{label[..2]}_{item.VideoId}",
                    start = ToMinutes(start),
                    end = ToMinutes(end),
                    genre = YouTubeService.MapCategoryToGenre(item.CategoryId, item.Title),
                    score = 50,
                    link = includeLink ? $"https://www.youtube.com/watch?v={item.VideoId}" : null
                }
            }
            });
        }

        instance.channels_count = instance.channels.Count;
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

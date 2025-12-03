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
        int maxStreams = 1000;                   // fetch more streams → more potential channels
        int maxChannelsToUse = 40;             // cap channels used in instance
        bool includeLink = true;               // control whether link is included in JSON

        var yt = new YouTubeService(apiKey);
        var streams = await yt.GetLiveStreamsAsync(maxStreams);
        var live = await yt.GetLiveStreamsAsync('n', null, null, 20);
        Console.WriteLine("LIVE NOW: " + live.Count);


        var past = await yt.GetLiveStreamsAsync(
        'p',
        DateTime.UtcNow.AddHours(-4),
        DateTime.UtcNow,
        20);
        Console.WriteLine("PAST: " + past.Count);

        var future = await yt.GetLiveStreamsAsync(
        'f',
        DateTime.UtcNow,
        DateTime.UtcNow.AddDays(2),
        20);
        Console.WriteLine("FUTURE: " + future.Count);

        
        //var streams = past;
        if (streams.Count == 0)
        {
            Console.WriteLine("No live streams fetched.");
            return;
        }

        // Shuffle and take subset for channels
        var rand = new Random();
        // Shuffle livestreams randomly
        var shuffledStreams = streams.OrderBy(_ => rand.Next()).ToList();

        int programsPerChannel = 5; // ensure each channel has enough unique content

        var channelsData = new List<List<YouTubeVideo>>();
        for (int i = 0; i < maxChannelsToUse; i++)
        {
            channelsData.Add(new List<YouTubeVideo>());
        }

        // Assign streams evenly first
        int idx = 0;
        foreach (var s in shuffledStreams)
        {
            channelsData[idx % maxChannelsToUse].Add(s);
            idx++;
        }

        // Ensure every channel has enough streams
        for (int c = 0; c < maxChannelsToUse; c++)
        {
            while (channelsData[c].Count < programsPerChannel)
            {
                // Pick another random stream (but different than the one already assigned)
                var extra = shuffledStreams[rand.Next(shuffledStreams.Count)];

                if (!channelsData[c].Any(x => x.VideoId == extra.VideoId))
                {
                    channelsData[c].Add(extra);
                }
            }
        }

        int channelCount = maxChannelsToUse;

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

        foreach (var channelPrograms in channelsData)
        {
            if (channelPrograms.Count == 0) continue;

            string baseGenre = YouTubeService.MapCategoryToGenre(
                channelPrograms.First().CategoryId,
                channelPrograms.First().Title);

            var channel = new Channel
            {
                channel_id = channelId,
                channel_name = $"Channel {channelId + 1}",
                programs = new List<ProgramItem>()
            };

            int currentTime = opening;
            int programIndex = 0;

            while (currentTime < closing)
            {
                foreach (var prog in channelPrograms)
                {
                    if (currentTime >= closing)
                        break;

                    int remaining = closing - currentTime;
                    if (remaining < minDuration)
                        break;

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
                        program_id = $"CH{channelId}_P{programIndex}",
                        start = start,
                        end = end,
                        genre = PickProgramGenre(rand, baseGenre),
                        score = rand.Next(40, 101),
                        link = includeLink
                            ? $"https://www.youtube.com/watch?v={prog.VideoId}"
                            : null
                    });

                    programIndex++;
                    currentTime = end;
                }
            }

            instance.channels.Add(channel);
            channelId++;
        }

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

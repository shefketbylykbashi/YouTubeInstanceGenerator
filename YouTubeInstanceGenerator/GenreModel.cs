using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeInstanceGenerator
{
    public static class GenreModel
    {
        private static readonly string[] FallbackGenres =
        {
        "news","sports","music","talk","movie","documentary",
        "kids","tech","gaming","variety"
    };

        public static string ResolveGenre(YouTubeVideo v)
        {
            string t = v.Title.ToLowerInvariant();

            // title-driven
            if (t.Contains("news")) return "news";
            if (t.Contains("match") || t.Contains("football") || t.Contains("sports"))
                return "sports";
            if (t.Contains("music") || t.Contains("dj")) return "music";
            if (t.Contains("podcast") || t.Contains("talk")) return "talk";
            if (t.Contains("documentary")) return "documentary";
            if (t.Contains("kids") || t.Contains("cartoon")) return "kids";
            if (t.Contains("game") || t.Contains("gaming")) return "gaming";
            if (t.Contains("tech") || t.Contains("review")) return "tech";
            if (t.Contains("movie") || t.Contains("film")) return "movie";

            // fallback random
            var rand = new Random();
            return FallbackGenres[rand.Next(FallbackGenres.Length)];
        }
    }

}

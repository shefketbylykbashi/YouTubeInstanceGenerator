using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeInstanceGenerator.Models
{
    public class TimePreference
    {
        public int start { get; set; }
        public int end { get; set; }
        public string preferred_genre { get; set; }
        public int bonus { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeInstanceGenerator.Models
{
    public class Instance
    {
        public int opening_time { get; set; }
        public int closing_time { get; set; }
        public int min_duration { get; set; }
        public int max_consecutive_genre { get; set; }
        public int channels_count { get; set; }
        public int switch_penalty { get; set; }
        public int termination_penalty { get; set; }
        public List<PriorityBlock> priority_blocks { get; set; }
        public List<TimePreference> time_preferences { get; set; }
        public List<Channel> channels { get; set; }
    }

}

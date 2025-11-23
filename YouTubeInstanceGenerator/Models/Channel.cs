using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeInstanceGenerator.Models
{
    public class Channel
    {
        public int channel_id { get; set; }
        public string channel_name { get; set; }
        public List<ProgramItem> programs { get; set; }
    }
}

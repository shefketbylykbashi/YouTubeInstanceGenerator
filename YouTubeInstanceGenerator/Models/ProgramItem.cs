using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeInstanceGenerator.Models
{
    public class ProgramItem
    {
        public string program_id { get; set; }
        public int start { get; set; }
        public int end { get; set; }
        public string genre { get; set; }
        public int score { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? link { get; set; }
    }
}

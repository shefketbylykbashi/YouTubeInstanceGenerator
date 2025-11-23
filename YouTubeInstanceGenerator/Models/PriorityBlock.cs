using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeInstanceGenerator.Models
{
    public class PriorityBlock
    {
        public int start { get; set; }
        public int end { get; set; }
        public List<int> allowed_channels { get; set; }
    }
}

# 📺 YouTube Instance Generator  
### 🎥 Generate Scheduling Instances from YouTube Live / Upcoming / Past Streams (C# / .NET)

This CLI tool fetches livestream metadata from **YouTube Data API v3** and generates an **Instance JSON** file used for AI-based TV scheduling automation.

It supports:
✔ Live streams (`--mode=n`)  
✔ Future scheduled streams (`--mode=f`)  
✔ Past streams (`--mode=p`)  
✔ Dynamic JSON + CSV exporting  
✔ Genre mapping + structured time preferences  
✔ Adjustable number of channels  
✔ Optional inclusion of YouTube video URLs  

All generated files are saved automatically into the **Output/** folder.

---

## 🛠️ Installation & Requirements

### 📌 Prerequisites

Before running this tool, ensure that:
- .NET SDK **8.0 or newer** is installed  
  👉 https://dotnet.microsoft.com/download
- You have a valid **YouTube Data API v3 key**  
  👉 https://console.cloud.google.com/apis/credentials

### ✔️ Verify .NET Installation
```bash
dotnet --version
```
If installed successfully, this prints the version (e.g. `8.0.x`)

---

## 🚀 Building & Running

Clone the repository and navigate into the project folder:

```bash
git clone <your_repo_url>
cd YouTubeInstanceGenerator
```

Restore dependencies:
```bash
dotnet restore
```

Run the application with parameters:
```bash
dotnet run -- <parameters>
```

---

## ⚙️ Command-Line Parameters

| Parameter | Required | Description | Example |
|----------|:--------:|-------------|--------|
| `--apikey` | ✅ Yes | YouTube API key for fetching streams | `--apikey=AIzaSyDxxxx` |
| `--mode` | ✅ Yes | Stream mode: `n = now`, `f = future`, `p = past` | `--mode=f` |
| `--maxchannels` | No | Max channels in output instance | `--maxchannels=50` |
| `--includeLink` | No | Include YouTube video URL (default: true) | `--includeLink=false` |
| `--start` | Required only for `f` or `p` | Fetch start time (`yyyyMMddHHmm`) | `--start=202512062300` |
| `--end` | Required only for `f` or `p` | Fetch end time (`yyyyMMddHHmm`) | `--end=202512092300` |

---

## 🧠 Usage Examples

### 1️⃣ Generate LIVE NOW Instance
```bash
dotnet run -- \
--apikey=AIzaSyD \
--mode=n \
--maxchannels=100 \
--includeLink=true
```

Output:
```
Output/instance_live_20251207_001009.json
Output/urls_20251207_001009.csv
```

---

### 2️⃣ Generate UPCOMING Instance with Date Range
```bash
dotnet run -- \
--apikey=AIzaSyDtvEc \
--mode=f \
--maxchannels=80 \
--start=202512062300 \
--end=202512092300 \
--includeLink=true
```

---

### 3️⃣ Generate PAST Instance
```bash
dotnet run -- \
--apikey=AIzaSyAwC \
--mode=p \
--start=202511300000 \
--end=202512012359 \
--includeLink=false
```

---

## 📂 Output Files

Files saved to:
```
Output/
```

| File | Mode | Details |
|------|------|---------|
| `instance_live_<timestamp>.json` | Live | Multi-program dynamic streams |
| `instance_upcoming_<timestamp>.json` | Future | Single-program scheduled streams |
| `instance_past.json` | Past | Historical livestreams with end times |
| `urls_<timestamp>.csv` | Live/Future | Channel list + YouTube URLs |

Used for:
🎯 AI-based scheduling  
📡 EPG simulation  
📊 Content planning  

---

## 🧩 JSON Instance Structure Example

```json
{
  "opening_time": 0,
  "closing_time": 1440,
  "channels_count": 40,
  "min_duration": 30,
  "max_consecutive_genre": 2,
  "switch_penalty": 3,
  "termination_penalty": 15,
  "priority_blocks": [],
  "time_preferences": [],
  "channels": [
    {
      "channel_id": 0,
      "channel_name": "Example Channel",
      "programs": [...]
    }
  ]
}
```

---

## 📡 YouTube API Quotas

Fetching streams consumes API quota.  
To avoid limits:
- Monitor API usage in Google Cloud Console
- Reduce `maxchannels`
- Limit date ranges

---

## 🐛 Troubleshooting

| Issue | Cause | Fix |
|------|------|----|
| `Missing required parameter: --apiKey` | Forgot API key | Provide `--apikey=<key>` |
| `Missing required date params!` | UPCOMING/PAST require time range | Add `--start` and `--end` |
| No results found | API region/filters might be restrictive | Expand time range |

---

## 🏗️ Future Enhancements
Planned roadmap:
- ⏱ More accurate program duration from metadata
- 🧠 ML genre inference
- 📊 Export to Excel (.xlsx)
- 🐳 Docker image
- 🖥 GUI for managing instances
- 🌍 Multi-region parallel querying

---

## 👨‍💻 Author
**Shefket Bylykbashi**  
Master’s in Computer & Software Engineering  
University of Prishtina  

---

If you'd like, I can also add:

✔ Badges (license, build, .NET version)  
✔ Architecture diagram  
✔ Screenshots & CSV preview  
✔ GitHub wiki pages  
✔ Publish as .NET Global Tool (`dotnet tool install`)  
✔ Dockerfile & CI/CD pipeline

Just tell me — happy to help 🚀

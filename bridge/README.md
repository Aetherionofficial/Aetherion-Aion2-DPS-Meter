# Aetherion TikTok Live Bridge

This optional bridge connects a TikTok LIVE username to Aetherion's verified local
webhook receiver. It uses the community `tiktok-live-connector` package, which reads
TikTok's internal Webcast stream and is therefore unofficial and subject to breakage
when TikTok changes that protocol.

Install and run it from this folder:

```powershell
npm install
$env:AETHERION_TIKTOK_USERNAME = "dein_streamer_name"
$env:AETHERION_TIKTOK_WEBHOOK_URL = "https://dein-relay.example/aetherion/"
$env:AETHERION_TIKTOK_CLIENT_SECRET = "dein-geheimer-schlüssel"
npm start
```

The webhook URL must reach Aetherion's local receiver through a public HTTPS relay or
reverse proxy. Never commit the client secret or put it into the repository.

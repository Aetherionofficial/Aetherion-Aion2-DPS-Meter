# Aetherion Live

Aetherion Live is a standalone Windows app, separate from the Aetherion DPS Meter. It
supports gifts, follows, likes, shares, subscriptions and chat events, with rule
matching, custom sound cues, speech, templates and duplicate-event protection.

## TikTok webhook bridge

The desktop app can receive verified TikTok webhook requests through a local
`HttpListener`. TikTok cannot call a desktop-only `localhost` URL directly, so a public
HTTPS relay or reverse proxy must forward the request to the configured local prefix.

The receiver is opt-in and reads these environment variables:

- `AETHERION_TIKTOK_WEBHOOK_PREFIX`, for example `http://localhost:39171/aetherion/`
- `AETHERION_TIKTOK_CLIENT_SECRET`, kept outside `settings.json`

Also enable `LiveAlertsEnabled` in the app settings. The receiver checks the
`TikTok-Signature` HMAC-SHA256 header and rejects missing, invalid or stale requests
before parsing the event. No `user_openid` is shown in an alert.

The supported normalized event names are `live.gift`, `live.follow`, `live.like`,
`live.share`, `live.subscribe` and `live.comment`. Unknown payloads are acknowledged
but ignored, so a new TikTok event cannot crash the app.

The app's Live-Alerts window remains usable without a stream through its local test
mode.

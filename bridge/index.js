const crypto = require('node:crypto');
const { TikTokLiveConnection } = require('tiktok-live-connector');

const username = process.env.AETHERION_TIKTOK_USERNAME;
const webhookUrl = process.env.AETHERION_TIKTOK_WEBHOOK_URL;
const secret = process.env.AETHERION_TIKTOK_CLIENT_SECRET;

if (!username || !webhookUrl || !secret) {
  console.error('Set AETHERION_TIKTOK_USERNAME, AETHERION_TIKTOK_WEBHOOK_URL and AETHERION_TIKTOK_CLIENT_SECRET.');
  process.exit(2);
}

const connection = new TikTokLiveConnection(username.replace(/^@/, ''));
let sequence = 0;

async function publish(event, details) {
  const body = JSON.stringify({
    event,
    event_id: `${Date.now()}-${sequence++}`,
    content: JSON.stringify(details)
  });
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const signature = crypto.createHmac('sha256', secret).update(`${timestamp}.${body}`).digest('hex');
  const response = await fetch(webhookUrl, {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'TikTok-Signature': `t=${timestamp},s=${signature}` },
    body
  });
  if (!response.ok) throw new Error(`Aetherion webhook returned ${response.status}`);
}

function user(data) {
  return data.uniqueId || data.user?.uniqueId || data.nickname || data.user?.nickname || 'Zuschauer';
}

function safePublish(event, details) {
  publish(event, details).catch(error => console.error(`[${event}] ${error.message}`));
}

connection.on('chat', data => safePublish('live.comment', { user_name: user(data), message: data.comment || '' }));
connection.on('gift', data => safePublish('live.gift', {
  user_name: user(data), gift_name: data.giftName || data.gift?.name || 'Geschenk',
  quantity: data.repeatCount || data.repeatCount2 || 1, value: data.diamondCount || 0
}));
connection.on('follow', data => safePublish('live.follow', { user_name: user(data) }));
connection.on('share', data => safePublish('live.share', { user_name: user(data) }));
connection.on('like', data => safePublish('live.like', { user_name: user(data), quantity: data.likeCount || 1 }));
connection.on('subscribe', data => safePublish('live.subscribe', { user_name: user(data) }));
connection.on('streamEnd', () => console.log('TikTok LIVE ended.'));
connection.on('disconnected', reason => console.warn(`TikTok LIVE disconnected: ${reason || 'unknown reason'}`));

async function connect() {
  try {
    const state = await connection.connect();
    console.log(`Connected to TikTok LIVE room ${state.roomId}.`);
  } catch (error) {
    console.error(`TikTok LIVE connection failed: ${error.message}`);
    process.exitCode = 1;
  }
}

process.on('SIGINT', () => { connection.disconnect(); process.exit(0); });
connect();

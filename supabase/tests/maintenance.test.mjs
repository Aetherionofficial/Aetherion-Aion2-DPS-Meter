import { readFileSync } from 'node:fs';
import { stripTypeScriptTypes } from 'node:module';
import vm from 'node:vm';
import assert from 'node:assert/strict';

function load(name, client, ownerIds = 'owner') {
  let handler;
  const source = readFileSync(new URL(`../functions/${name}/index.ts`, import.meta.url), 'utf8').replace(/^import .*;\r?\n/, '');
  vm.runInNewContext(stripTypeScriptTypes(source), {
    Response, Request, URL, console, createClient: () => client,
    Deno: { env: { get: key => key === 'AETHERION_LICENSE_ADMIN_USER_IDS' ? ownerIds : 'configured' }, serve: fn => handler = fn },
  });
  return handler;
}
const state = { enabled: false, message: 'Wartung', revision: 0, updated_at: '2026-10-18' };
let failRead = false, calls = [], user = 'owner', conflict = false;
const db = {
  auth: { getUser: async () => ({ data: { user: { id: user } } }) },
  from: () => ({ select: () => ({ eq: () => ({ single: async () => failRead ? { error: 'offline' } : { data: state } }) }) }),
  rpc: async (name, args) => { calls.push([name, args]); return conflict ? { error: { message: 'MAINTENANCE_CONFLICT' } } : { data: { enabled: args.p_enabled, message: args.p_message, revision: 1 } }; },
};
const status = load('aetherion-app-status', db);
assert.equal((await status(new Request('https://test'))).status, 200);
assert.equal((await (await status(new Request('https://test'))).json()).enabled, false);
failRead = true;
assert.equal((await status(new Request('https://test'))).status, 503);
failRead = false;
assert.equal((await status(new Request('https://test', { method: 'POST' }))).status, 405);

const admin = load('aetherion-license-admin', db);
const post = body => new Request('https://test', { method: 'POST', headers: { Authorization: 'Bearer test', 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
const valid = { action: 'set_maintenance', enabled: true, message: ' Wartung ', expectedRevision: 0, actor_user_id: 'spoofed' };
assert.equal((await admin(new Request('https://test?view=maintenance'))).status, 401);
user = 'customer';
assert.equal((await admin(post(valid))).status, 403);
assert.equal(calls.length, 0);
user = 'owner';
assert.equal((await admin(post({ ...valid, expectedRevision: -1 }))).status, 400);
assert.equal((await admin(post({ ...valid, enabled: 'true' }))).status, 400);
assert.equal((await admin(post({ ...valid, message: ' '.repeat(501) }))).status, 400);
assert.equal((await admin(post(valid))).status, 200);
assert.equal(calls[0][1].p_actor_user_id, 'owner');
assert.equal(calls[0][1].p_message, 'Wartung');
conflict = true;
assert.equal((await admin(post(valid))).status, 409);

const check = load('aetherion-license-check', db);
const accessRequest = () => new Request('https://test', { method: 'POST', headers: { Authorization: 'Bearer test' }, body: JSON.stringify({ installationId: 'a'.repeat(24) }) });
state.enabled = true; calls = [];
const blocked = await check(accessRequest());
assert.equal(blocked.status, 503);
assert.equal((await blocked.json()).error, 'app_maintenance');
assert.equal(calls.length, 0, 'Maintenance must not bind or revoke devices');
state.enabled = false; conflict = false;
db.rpc = async name => { calls.push(name); return { data: true }; };
assert.equal((await check(accessRequest())).status, 200);
assert.deepEqual(calls, ['check_aetherion_account_installation']);
console.log('Maintenance edge-function checks passed: public status, owner authorization, validation, conflict, audit actor, installation guard.');

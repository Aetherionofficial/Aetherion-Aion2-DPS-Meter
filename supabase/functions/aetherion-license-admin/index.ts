import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Cache-Control": "no-store",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type, x-client-info",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
};
const json = (body: unknown, status = 200) =>
  Response.json(body, { status, headers: corsHeaders });
const allowedTypes = new Set(["trial_1d", "week_1w", "days_30", "days_90", "days_365", "lifetime", "partner"]);
const durationDays: Record<string, number | null> = {
  trial_1d: 1, week_1w: 7, days_30: 30, days_90: 90, days_365: 365, lifetime: null, partner: null,
};

Deno.serve(async (request: Request) => {
  if (request.method === "OPTIONS") return new Response("ok", { headers: corsHeaders });
  if (request.method !== "GET" && request.method !== "POST") return json({ error: "method_not_allowed" }, 405);

  try {
    const token = request.headers.get("Authorization")?.match(/^Bearer\s+(.+)$/i)?.[1];
    if (!token) return json({ error: "auth_required" }, 401);
    const url = Deno.env.get("SUPABASE_URL");
    const serviceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
    if (!url || !serviceKey) return json({ error: "admin_backend_not_configured" }, 503);
    const adminIds = new Set((Deno.env.get("AETHERION_LICENSE_ADMIN_USER_IDS") ?? "")
      .split(",").map((id) => id.trim()).filter(Boolean));

    const supabase = createClient(url, serviceKey, {
      auth: { persistSession: false, autoRefreshToken: false },
    });
    const { data: auth, error: authError } = await supabase.auth.getUser(token);
    if (authError || !auth.user) return json({ error: "auth_required" }, 401);
    if (adminIds.size === 0) return json({ error: "license_admin_not_configured" }, 503);
    if (!adminIds.has(auth.user.id)) return json({ error: "owner_access_required" }, 403);

    if (request.method === "GET" && new URL(request.url).searchParams.get("view") === "maintenance") {
      const { data, error } = await supabase.from("aetherion_app_maintenance")
        .select("enabled,message,revision,updated_at").eq("id", true).single();
      if (error || !data) throw error ?? new Error("Maintenance state missing");
      return json({ enabled: data.enabled, message: data.message, revision: data.revision, updatedAt: data.updated_at });
    }

    if (request.method === "GET" && new URL(request.url).searchParams.get("view") === "history") {
      const { data: events, error } = await supabase.from("aetherion_license_admin_events")
        .select("created_at,action,key_hash,account_user_id,details,actor_user_id")
        .order("created_at", { ascending: false }).limit(250);
      if (error) throw error;
      const users = await listUsers(supabase);
      const byId = new Map(users.map((user) => [user.id, user]));
      const { data: maintenanceEvents, error: maintenanceHistoryError } = await supabase.from("aetherion_maintenance_events")
        .select("created_at,actor_user_id,enabled,message,revision").order("created_at", { ascending: false }).limit(250);
      if (maintenanceHistoryError) throw maintenanceHistoryError;
      const maintenanceHistory = (maintenanceEvents ?? []).map((event) => ({
        createdAt: event.created_at, action: event.enabled ? "Wartung aktiviert / aktualisiert" : "Wartung beendet",
        fingerprint: "APP", accountName: null,
        details: `${event.message} � Revision ${event.revision} � durch ${byId.get(event.actor_user_id)?.user_metadata?.name ?? "Owner"}`,
      }));
      return json({ events: [...maintenanceHistory, ...(events ?? []).map((event) => {
        const metadata = (event.account_user_id ? byId.get(event.account_user_id)?.user_metadata : null) ?? {};
        const actorMetadata = byId.get(event.actor_user_id)?.user_metadata ?? {};
        const accountName = metadata.global_name ?? metadata.full_name ?? metadata.name ?? metadata.preferred_username ??
          (event.account_user_id ? `Discord-Konto ${event.account_user_id.slice(0, 8)}` : null);
        const actorName = actorMetadata.global_name ?? actorMetadata.full_name ?? actorMetadata.name ?? "Owner";
        const actionNames: Record<string, string> = {
          key_created: "Key erzeugt", key_revoked: "Zugang gesperrt",
          key_restored: "Zugang freigegeben", device_reset: "Ger채tebindung zur체ckgesetzt",
        };
        return {
          createdAt: event.created_at,
          action: actionNames[event.action] ?? event.action,
          fingerprint: event.key_hash.slice(0, 12).toUpperCase(),
          accountName,
          details: [event.details, `durch ${actorName}`].filter(Boolean).join(" 쨌 "),
        };
      })].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt)).slice(0, 250) });
    }

    if (request.method === "GET") {
      const { data: keys, error: keysError } = await supabase
        .from("aetherion_license_keys")
        .select("key_hash,duration_days,license_type,issued_at,redeemed_at,redeemed_by,revoked_at")
        .order("issued_at", { ascending: false })
        .limit(1000);
      if (keysError) throw keysError;

      const [{ data: entitlements, error: entitlementsError }, { data: installations, error: installationsError }, users] = await Promise.all([
        supabase.from("aetherion_entitlements")
          .select("user_id,expires_at,is_revoked,license_type,is_partner,revoked_at"),
        supabase.from("aetherion_account_installations").select("user_id,installation_id,state"),
        listUsers(supabase),
      ]);
      if (entitlementsError) throw entitlementsError;
      if (installationsError) throw installationsError;
      const entitlementByUser = new Map((entitlements ?? []).map((row) => [row.user_id, row]));
      const installationByUser = new Map((installations ?? []).map((row) => [row.user_id, row]));
      const userById = new Map(users.map((user) => [user.id, user]));
      const now = Date.now();
      const rows = (keys ?? []).map((key) => {
        const entitlement = key.redeemed_by ? entitlementByUser.get(key.redeemed_by) : null;
        const installation = key.redeemed_by ? installationByUser.get(key.redeemed_by) : null;
        const user = key.redeemed_by ? userById.get(key.redeemed_by) : null;
        const metadata = user?.user_metadata ?? {};
        const accountName = metadata.global_name ?? metadata.full_name ?? metadata.name ??
          metadata.preferred_username ?? (key.redeemed_by ? `Discord-Konto ${key.redeemed_by.slice(0, 8)}` : null);
        let status = "available";
        if (key.revoked_at) status = "revoked";
        else if (key.redeemed_at) {
          if (entitlement?.is_revoked) status = "revoked";
          else if (entitlement?.expires_at && Date.parse(entitlement.expires_at) <= now) status = "expired";
          else status = "active";
        }
        return {
          keyHash: key.key_hash,
          fingerprint: key.key_hash.slice(0, 12).toUpperCase(),
          licenseType: key.license_type,
          durationDays: key.duration_days,
          issuedAt: key.issued_at,
          redeemedAt: key.redeemed_at,
          accountName,
          accountId: key.redeemed_by,
          expiresAt: entitlement?.expires_at ?? null,
          status,
          installationState: installation?.state ?? null,
          installationFingerprint: installation?.installation_id?.slice(0, 6).toUpperCase() ?? null,
        };
      });
      return json({ keys: rows, truncated: rows.length === 1000 });
    }

    const body = await request.json().catch(() => null);
    if (body?.action === "set_maintenance") {
      if (typeof body.enabled !== "boolean" || typeof body.message !== "string" ||
          !body.message.trim() || body.message.trim().length > 500 ||
          !Number.isSafeInteger(body.expectedRevision) || body.expectedRevision < 0)
        return json({ error: "invalid_maintenance_request" }, 400);
      const { data, error } = await supabase.rpc("admin_set_aetherion_maintenance", {
        p_enabled: body.enabled, p_message: body.message.trim(),
        p_expected_revision: body.expectedRevision, p_actor_user_id: auth.user.id,
      });
      if (error?.message?.includes("MAINTENANCE_CONFLICT")) return json({ error: "maintenance_conflict" }, 409);
      if (error) throw error;
      return json(data);
    }

    if (!body || typeof body.action !== "string") return json({ error: "invalid_request" }, 400);

    if (body.action === "create") {
      const type = String(body.licenseType ?? "");
      const count = Number(body.count ?? 1);
      if (!allowedTypes.has(type)) return json({ error: "license_type_invalid" }, 400);
      if (!Number.isInteger(count) || count < 1 || count > 50) return json({ error: "batch_size_invalid" }, 400);
      const { data, error } = await supabase.rpc("admin_issue_aetherion_license_keys", {
        p_license_type: type,
        p_count: count,
      });
      if (error) throw error;
      await recordEvents(supabase, (data ?? []).map((row: { key_hash: string; license_key: string; issued_at: string }) => ({
        actor_user_id: auth.user.id, action: "key_created", key_hash: row.key_hash, account_user_id: null,
        details: type,
      })));
      return json({ issued: (data ?? []).map((row: { license_key: string; key_hash: string; issued_at: string }) => ({
        key: row.license_key,
        fingerprint: row.key_hash.slice(0, 12).toUpperCase(),
        licenseType: type,
        durationDays: durationDays[type],
        issuedAt: row.issued_at,
      })) }, 201);
    }

    if (body.action === "revoke") {
      const keyHash = String(body.keyHash ?? "").toLowerCase();
      if (!/^[0-9a-f]{64}$/.test(keyHash)) return json({ error: "key_hash_invalid" }, 400);
      const { data, error } = await supabase.rpc("admin_revoke_aetherion_license_key_hash", {
        p_key_hash: keyHash,
      });
      if (error) throw error;
      if (data === true) await recordSingleEvent(supabase, auth.user.id, keyHash, "key_revoked", "Zugang gesperrt");
      return data === true ? json({ ok: true }) : json({ error: "key_not_found_or_already_revoked" }, 404);
    }

    if (body.action === "restore") {
      const keyHash = String(body.keyHash ?? "").toLowerCase();
      if (!/^[0-9a-f]{64}$/.test(keyHash)) return json({ error: "key_hash_invalid" }, 400);
      const { data, error } = await supabase.rpc("admin_restore_aetherion_license_key_hash", {
        p_key_hash: keyHash,
      });
      if (error) throw error;
      if (data === true) await recordSingleEvent(supabase, auth.user.id, keyHash, "key_restored", "Zugang freigegeben");
      return data === true ? json({ ok: true }) : json({ error: "key_not_found_or_not_revoked" }, 404);
    }

    if (body.action === "reset_device") {
      const keyHash = String(body.keyHash ?? "").toLowerCase();
      if (!/^[0-9a-f]{64}$/.test(keyHash)) return json({ error: "key_hash_invalid" }, 400);
      const { data, error } = await supabase.rpc("admin_reset_aetherion_account_device", { p_key_hash: keyHash });
      if (error) throw error;
      if (data === true) await recordSingleEvent(supabase, auth.user.id, keyHash, "device_reset", "Ger채tebindung zur체ckgesetzt");
      return data === true ? json({ ok: true }) : json({ error: "key_not_found_or_device_not_bound" }, 404);
    }

    return json({ error: "action_invalid" }, 400);
  } catch (error) {
    // Do not log request bodies or raw license keys; return a generic error to clients.
    console.error("Aetherion license admin request failed", error instanceof Error ? error.message : "unknown error");
    return json({ error: "license_admin_request_failed" }, 500);
  }
});

async function listUsers(client: ReturnType<typeof createClient>) {
  const users: Array<{ id: string; user_metadata?: Record<string, unknown> }> = [];
  for (let page = 1; page <= 10; page++) {
    const { data, error } = await client.auth.admin.listUsers({ page, perPage: 500 });
    if (error) throw error;
    users.push(...data.users);
    if (data.users.length < 500) break;
  }
  return users;
}

async function recordSingleEvent(
  client: ReturnType<typeof createClient>,
  actorId: string,
  keyHash: string,
  action: string,
  details: string,
) {
  const { data: key } = await client.from("aetherion_license_keys").select("redeemed_by").eq("key_hash", keyHash).maybeSingle();
  await recordEvents(client, [{ actor_user_id: actorId, action, key_hash: keyHash, account_user_id: key?.redeemed_by ?? null, details }]);
}

async function recordEvents(
  client: ReturnType<typeof createClient>,
  rows: Array<{ actor_user_id: string; action: string; key_hash: string; account_user_id: string | null; details: string }>,
) {
  if (rows.length === 0) return;
  const { error } = await client.from("aetherion_license_admin_events").insert(rows);
  if (error) console.warn("Aetherion admin audit write failed", error.message);
}

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const headers = { "Access-Control-Allow-Origin": "*", "Cache-Control": "no-store, max-age=0" };
Deno.serve(async (request: Request) => {
  if (request.method === "OPTIONS") return new Response("ok", { headers });
  if (request.method !== "GET") return Response.json({ error: "method_not_allowed" }, { status: 405, headers });
  try {
    const db = createClient(Deno.env.get("SUPABASE_URL")!, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
      { auth: { persistSession: false, autoRefreshToken: false } });
    const { data, error } = await db.from("aetherion_app_maintenance")
      .select("enabled,message,revision,updated_at").eq("id", true).single();
    if (error || !data) throw new Error("status_unavailable");
    return Response.json({ enabled: data.enabled, message: data.message, revision: data.revision, updatedAt: data.updated_at }, { headers });
  } catch {
    return Response.json({ error: "status_unavailable" }, { status: 503, headers });
  }
});

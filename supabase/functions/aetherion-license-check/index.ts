import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

Deno.serve(async (request: Request) => {
  if (request.method === "OPTIONS") return new Response("ok", { headers: corsHeaders });
  if (request.method !== "POST") return Response.json({ error: "method_not_allowed" }, { status: 405, headers: corsHeaders });

  try {
    const authorization = request.headers.get("Authorization") ?? "";
    const token = authorization.match(/^Bearer\s+(.+)$/i)?.[1];
    const { installationId } = await request.json();
    if (!token || typeof installationId !== "string" || !/^[0-9a-fA-F]{24}$/.test(installationId)) {
      return Response.json({ error: "invalid_request" }, { status: 400, headers: corsHeaders });
    }

    const url = Deno.env.get("SUPABASE_URL")!;
    const serviceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
    const supabase = createClient(url, serviceKey);
    const { data: auth, error: authError } = await supabase.auth.getUser(token);
    if (authError || !auth.user) return Response.json({ error: "auth_required" }, { status: 401, headers: corsHeaders });

    const { data, error } = await supabase.rpc("check_aetherion_account_installation", {
      p_user_id: auth.user.id,
      p_installation_id: installationId,
    });
    if (error) throw error;
    if (data !== true) {
      return Response.json({ ok: false, error: "account_access_blocked" }, { status: 403, headers: corsHeaders });
    }
    return Response.json({ ok: true }, { headers: corsHeaders });
  } catch (error) {
    console.error("Aetherion account installation check failed", error);
    return Response.json({ error: "license_check_unavailable" }, { status: 500, headers: corsHeaders });
  }
});

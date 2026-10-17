# Aetherion account access

## Device sharing protection

The Windows app calls the `aetherion-license-check` Edge Function when it checks an active Discord entitlement. The account is bound to its first installation. A later check from a different installation revokes that entitlement automatically. After confirming the account owner, an administrator can clear a false positive and require a new key redemption with:

```sql
delete from public.aetherion_account_installations
where user_id = 'SUPABASE_AUTH_USER_UUID';
```

Deploy the migration and function from the repository root after linking the Supabase project:

```powershell
npx supabase db push
npx supabase functions deploy aetherion-license-check
```

The Edge Function verifies the user's Supabase access token. Never put the service-role key in the desktop client.

## One-time project setup

1. In the Supabase Dashboard, open **SQL Editor**, create a query, paste `aetherion-access.sql`, and run it. This was the initial account-login setup.
2. Run the full `aetherion-paid-keys.sql` in a new SQL Editor query. It upgrades the previous key schema without deleting existing accounts or key history.
3. Open **Authentication → URL Configuration → Redirect URLs** and add this exact local desktop callback:

   `http://127.0.0.1:43781/auth/callback/`

   The Discord application's OAuth2 redirect remains the Supabase callback URL shown under the Discord provider settings.
4. Make sure Discord is enabled under **Authentication → Providers → Discord** and that the credentials are saved.

## Issuing a key after payment

After a ticket purchase, issue one of these key types in **SQL Editor**:

```sql
select public.issue_aetherion_license_key('trial_1d'); -- 1-day trial
select public.issue_aetherion_license_key('week_1w');  -- 1 week
select public.issue_aetherion_license_key('days_30');  -- 30 days
select public.issue_aetherion_license_key('days_90');  -- 90 days
select public.issue_aetherion_license_key('days_365'); -- 1 year
select public.issue_aetherion_license_key('lifetime'); -- lifetime
select public.issue_aetherion_license_key('partner');  -- lifetime + Partner badge
```

Run only the one matching the purchase, copy the returned key, and send it privately in the buyer's Discord ticket. The database stores only its SHA-256 hash. Keys are single-use and bind to the Discord account that redeems them. A lifetime or partner entitlement cannot redeem another key. Partner is shown only for the `partner` type; regular accounts see their remaining days in the overlay.

To revoke an issued key (also cuts access if that key has already been redeemed), run:

```sql
select public.revoke_aetherion_license_key('PASTE-THE-KEY-HERE');
```

`true` means it was revoked; `false` means the key was unknown or already revoked. Keep the raw key private; it is not stored in the database after issuance.

The buyer signs into Aetherion with Discord and enters the key. The app shows remaining days in the start screen and Settings. Redeeming another key adds its days to the remaining entitlement. **Abmelden** removes the saved session and closes the app; the next launch allows Discord sign-in again.

The app stores only the refresh token, protected with Windows DPAPI for the current Windows account. Entitlements are read through row-level security; redemption is an authenticated database function which derives the account id from the Supabase JWT. The publishable key in the client is not a secret.

## Owner license dashboard

The owner-only desktop build includes a searchable license dashboard. It shows key fingerprint, plan, state, issue date, Discord account, current access expiry and a shortened device-binding fingerprint. Search accepts a Discord name/UUID, key fingerprint or full raw key; status filters include active, expiring within seven days, unused, revoked and expired. Summary cards include active accounts, unused keys, revoked/expired keys and keys expiring within seven days.

The owner can issue batches of up to 50 one-time keys, revoke unused or redeemed keys, restore revoked access, and reset a customer's device binding without changing the license state or expiry. Restoring a redeemed key clears the entitlement's revocation and old device binding so the next login can bind the intended installation; it does not extend the key's existing expiration. A private owner-only audit log records key creation, revocation, restoration and device resets. The full key is returned only once after generation; only its SHA-256 hash is stored. After copying generated keys, use **Anzeige leeren** to remove them from the dashboard; the clipboard contents remain available.

Deploy the database migration and Edge Function from the repository root:

```powershell
npx.cmd supabase db push --linked
npx.cmd supabase functions deploy aetherion-license-admin --project-ref aafdjzdirqpanuueveng
```

In **Supabase Dashboard → Authentication → Users**, copy the owner's Supabase user UUID after signing into Aetherion with Discord once. Allowlist only that UUID (multiple UUIDs may be comma-separated):

```powershell
npx supabase secrets set AETHERION_LICENSE_ADMIN_USER_IDS=YOUR-SUPABASE-AUTH-USER-UUID
```

Build and launch the owner-only dashboard from the repository root:

```powershell
dotnet run --project src/AionMeter.App/AionMeter.App.csproj -p:AetherionOwnerBuild=true -- --owner-license-dashboard
```

The first launch opens Discord sign-in if no Aetherion session is saved. The Edge Function checks the signed-in Supabase user against the server-side allowlist on every request. The service-role key remains in Supabase and is never placed in the desktop app. Regular customer builds compile out the dashboard and the existing local key issuer.

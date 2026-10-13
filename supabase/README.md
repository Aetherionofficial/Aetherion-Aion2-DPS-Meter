# Aetherion account access

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

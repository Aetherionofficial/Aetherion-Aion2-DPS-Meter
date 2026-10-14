# Aetherion DPS Meter v0.3.26 — Hotfix

## Deutsch — Hotfix / Bug Fixes
- **Nur echte DPS-Beiträge werden angezeigt:** Spieler erscheinen erst in der DPS-Liste, wenn im Kampf tatsächlich Schaden von ihnen erfasst wurde. Eine reine Nähe zur Gruppe oder Heilung allein erzeugt keine Null-DPS-Zeile mehr.
- **Heilung sauber zugeordnet:** Heilungsereignisse werden nur noch für Spieler gezählt, die in diesem Kampf auch Schaden beigetragen haben. So erzeugen fremde oder passive Heilungsereignisse keine Spielerzeile und keinen irreführenden Heilungswert.

## English — Hotfix / Bug Fixes
- **Only real damage contributors are shown:** Players appear in the DPS list only after damage from them has been recorded in the encounter. Being nearby or healing alone no longer creates a zero-DPS row.
- **Healing is attributed cleanly:** Healing events are counted only for players who have also dealt damage in that encounter. Nearby or passive healing events therefore cannot create a player row or a misleading healing total.

## Voraussetzungen / Requirements
- Npcap is required for live combat capture. Microsoft Edge WebView2 Runtime is required for the embedded map.
- Ein aktiver Aetherion-Zugang ist erforderlich. Support: [Discord](https://discord.gg/mostwantedde).
- This release does not include an installer.

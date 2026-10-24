# Aetherion DPS Meter v0.3.78

## Gesamtschaden im Kampf-Overlay

- Das Overlay zeigt pro Spieler jetzt zusätzlich zum DPS den kumulierten Gesamtschaden an.
- DPS und Gesamtschaden werden aus demselben kumulierten Schadenswert und der Kampfdauer abgeleitet.
- Große Werte werden kompakt und mit deutscher Zahlenformatierung dargestellt, zum Beispiel `843,2k` oder `1,25 Mio.`.
- Der prozentuale Anteil am Gruppenschaden bleibt sichtbar.

Validierung: Release-Build erfolgreich; die bestehende Schadens- und Reset-Logik wurde unverändert genutzt.

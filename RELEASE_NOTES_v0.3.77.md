# Aetherion DPS Meter v0.3.77

## Hotfix: Kampfdetails sichtbar öffnen

- Das über den Details-Button oder einen Spieler im DPS-Overlay geöffnete Analysefenster wird jetzt ausdrücklich im Vordergrund angezeigt. Zuvor konnte es hinter einem randlosen Spiel landen und der Button dadurch wirkungslos erscheinen.
- Ein minimiertes oder ausgeblendetes Detailfenster wird beim Klick auf den Details-Button wiederhergestellt, statt geschlossen oder ein zweites Mal geöffnet zu werden.
- Ist das Detailfenster bereits sichtbar, schließt ein weiterer Klick auf den Details-Button es weiterhin. Ein Klick auf einen anderen Spieler wechselt die vorhandene Auswertung und holt sie nach vorne.
- Das DPS-Overlay selbst bleibt nicht aktivierend; nur das ausdrücklich geöffnete Detailfenster darf den Fokus übernehmen.

Validierung: 34 App-Tests bestanden, einschließlich Öffnen mit Vordergrund-Einstellung, Wiederherstellen minimierter und ausgeblendeter Details sowie erneutem Schließen. Kein Live-Ingametest. DPS-Berechnung, Paketparser, Ping-Messung und Lizenz-/Login-Logik sind unverändert.

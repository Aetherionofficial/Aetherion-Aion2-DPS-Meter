# Aetherion DPS Meter v0.3.76

## Neues Kampf-Overlay

- Kompakte Gruppenliste mit bis zu sechs Spielern, Rang, Klassen-Icon, Name, ausgeschriebener DPS-Zahl und Schadensanteil. Sortierung nach DPS; der eigene Spieler bleibt sichtbar und ist türkis hervorgehoben.
- Sechs Platzhalterzeilen, solange noch keine Kampfdaten vorhanden sind. Lange Namen werden mit „…“ gekürzt.
- Farbige, halbtransparente Schadensbalken mit sanften Übergängen; der erste Spieler bildet die Referenz für die volle Balkenbreite.
- Kopfzeile mit „DPS-Statistiken“, Kampf-Timer und den Aktionen Details, Reset, Einstellungen und Schließen. Tooltips und Hover-Effekte helfen bei der Bedienung.
- Verschieben über die Kopfzeile; Größenänderung an allen Rändern und Ecken innerhalb der Mindest- und Maximalgröße.

## Spielerdetails direkt öffnen

- Der neue Details-Button öffnet die vorhandene Kampfanalyse und schließt sie beim erneuten Anklicken wieder.
- Standardmäßig wird der eigene Spieler ausgewählt. Ein Klick auf einen Spieler im Overlay oder die Spielerauswahl im Detailfenster wechselt die Auswertung.
- Gesamtschaden, Kampfdauer, DPS, Crit-Rate, nach Schaden sortierte Fähigkeiten mit Prozentanteilen sowie die vorhandene Heilungsanalyse.

## Einheitliche Skalierung und Transparenz

- Neuer Regler „Skalierung“ unter Einstellungen → Overlay: 70–150 % in 5-%-Schritten. Schrift, Icons, Zeilen und Abstände folgen einem gemeinsamen Faktor.
- Beim Ziehen der Fensterbreite bleiben die Schriftgrößen unverändert. Nur die verfügbare Breite für Namen und Balken verändert sich.
- Bei 100 % sind Spielerzeilen etwa 24 Pixel hoch. Kompakt / Standard / Groß wählen 85 / 100 / 125 % und sechs Spielerplätze.
- Hintergrund-Deckkraft bleibt separat einstellbar und gespeichert. Text und Icons bleiben lesbar; hoher Kontrast verwendet einen vollständig deckenden Hintergrund.

## Ping-Anzeige

- Numerischer Ping in Millisekunden unten rechts mit grünem, gelbem, orangem oder rotem Statuspunkt.
- Asynchrone ICMP-Messung alle zwei Sekunden, geglättet über die letzten fünf Messversuche. Ausfälle werden nicht als 0 ms gezählt; ohne Antwort erscheint „--“.
- Ziel ist die eindeutig erkannte AION2-Verbindung am bekannten Gameserver-Port. Ist kein eindeutiger Server erkennbar, wird 1.1.1.1 verwendet und in Einstellungen sowie Tooltip als Näherungswert gekennzeichnet.
- Der Tooltip zeigt die Messquelle sowie Minimum und Maximum der letzten Minute. Blockiert ein Gameserver ICMP, wird keine erfolgreiche Messung vorgetäuscht.
- „Ping anzeigen“ ist standardmäßig aktiv und lässt sich in den Overlay-Einstellungen ausschalten.

## Rendering-Korrektur und Prüfung

- Das transparente DPS-Fenster verwendet Software-Rendering, um mögliche Konflikte seiner WPF-Grafikbeschleunigung mit randlosen Spielen zu vermeiden. Ob dies das gemeldete sporadische Flackern des gesamten Spielbilds beseitigt, muss noch ingame bestätigt werden.
- Leere Spielerplätze werden bei laufenden Updates wiederverwendet, statt die gesamte Liste im Leerzustand ständig neu aufzubauen.
- 114 Core-Tests und 34 App-Tests bestanden, darunter Ping-Grenzwerte, Glättung und Abbruch, sechs Spielerplätze, DPS-Sortierung, Skalierung, Details, Hintergrund-Persistenz und stabile Fenstermaße bei wiederholten Updates.
- Echte WPF-Vorschauen verwenden Beispieldaten. Der darin gezeigte Ping von 42 ms ist ein Beispielwert.

DPS-Berechnung, Paketparser und Lizenz-/Login-Logik wurden nicht geändert.

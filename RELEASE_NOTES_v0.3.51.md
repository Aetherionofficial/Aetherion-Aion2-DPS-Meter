# Aetherion DPS Meter v0.3.51

## Updates direkt beim Start
- Vor Anmeldung, LizenzprÃ¼fung und Wartungsfenster prÃ¼ft die App auf eine neue Version. Ist eine neuere, nicht Ã¼bersprungene Version verfÃ¼gbar, Ã¶ffnet sich direkt das Update-Fenster mit den Neuerungen und â€žJetzt aktualisierenâ€œ.
- Installer- und Portable-Versionen kÃ¶nnen ihr passendes Update herunterladen und anschlieÃŸend neu starten. Es wird erst nach Klick auf die Update-SchaltflÃ¤che installiert. Bei fehlgeschlagener PrÃ¼fung bleibt die normale Lizenz- und WartungsprÃ¼fung aktiv.
- Auch das Wartungsfenster hat jetzt â€žNach Updates suchenâ€œ. Ein Update ist damit wÃ¤hrend der Wartung erreichbar; die Wartungssperre bleibt bestehen. Nach dem Update prÃ¼ft die App den aktuellen Status erneut.

## Wartungsmodus fÃ¼r die Hauptapp
- Der private Admin-Bereich kann die Hauptapp in Wartung setzen, einen Hinweistext Ã¤ndern und die App anschlieÃŸend wieder freigeben. Nur freigeschaltete Administratoren dÃ¼rfen Ã„nderungen vornehmen. Ã„nderungen werden protokolliert.
- Die Kunden-App prÃ¼ft den Wartungsstatus beim Start und etwa alle 15 Sekunden wÃ¤hrend der Nutzung. Bei Wartung werden Erfassung, Demo, Replay und Bedienung gesperrt. Das Fenster â€žWartungsarbeitenâ€œ erklÃ¤rt, dass die App vorÃ¼bergehend nicht genutzt werden kann.
- Nach Freigabe kann die App automatisch neu starten und den Lizenzzugang erneut prÃ¼fen. Der Admin-Zugang bleibt erreichbar. Keys, GerÃ¤tebindungen und Lizenzlaufzeiten werden durch Wartung nicht verÃ¤ndert.
- Eine bereits erkannte Wartungssperre bleibt bei Verbindungsfehlern bestehen. Eine kurzzeitige StÃ¶rung im Betrieb wird nach bestÃ¤tigter Freigabe bis zu zwei Minuten Ã¼berbrÃ¼ckt, danach erscheint eine Verbindungssperre.
- Die technische JSON-Ausgabe bei Wartung wurde durch einen verstÃ¤ndlichen Hinweis ersetzt; auch Ã¤ltere Anmeldefenster erhalten nun einen lesbaren Servertext.

**Einmaliges Update erforderlich:** Ã„ltere Versionen erhalten das neue Start-Updatefenster und die Wartungssperre erst durch Installation dieser Version. Wer wÃ¤hrend der Wartung in einer alten App festhÃ¤ngt, lÃ¤dt den Installer oder die ZIP aus diesem Release herunter. Die neuen Wartungs-/Update-Hinweise sind zunÃ¤chst deutsch. Das Update beendet keine laufende Wartung.

Alle bisherigen Funktionen aus v0.3.49 bleiben enthalten, einschlieÃŸlich des kompakten Overlays, der Layout-Profile und des Teilens von DPS-Ergebnissen ausschlieÃŸlich Ã¼ber die Discord-Tabelle.

# Aetherion DPS Meter v0.3.50

- Neuer globaler Wartungsmodus im privaten Admin-Dashboard mit eigenem Kundentext, Aktivierungsbestätigung und Freigabe-Schaltfläche.
- Beim Start und im laufenden Betrieb prüft die Hauptapp den Wartungsstatus. Bei aktiver Wartung stoppt die Nutzung und ein Fenster „Wartungsarbeiten“ erscheint. Laufende Apps prüfen ungefähr alle 15 Sekunden; Netzwerklaufzeit kommt hinzu.
- Nach der Freigabe kann die App automatisch neu starten und den Lizenzzugang erneut prüfen. Schließen des Wartungsfensters beendet die App.
- Der Admin-Zugang bleibt während der Wartung erreichbar. Nur serverseitig freigeschaltete Administratoren können den Status ändern; Änderungen werden protokolliert und vor gegenseitigem Überschreiben geschützt.
- Die Wartung sperrt keine Keys, verändert keine Gerätebindungen und verlängert oder verkürzt keine Lizenzlaufzeiten. Sie ist standardmäßig ausgeschaltet.
- Kurze Verbindungsprobleme im Betrieb werden bis zu zwei Minuten überbrückt. Danach erscheint eine Verbindungssperre. Eine erkannte Wartung wird bei Verbindungsfehlern nicht aufgehoben.

Die neue Kunden-App muss installiert sein, damit Wartungsfenster und Nutzungssperre greifen. Ältere App-Versionen erhalten diese Funktion nicht allein durch das Backend-Update. Die neue Wartungsoberfläche ist zunächst deutsch.

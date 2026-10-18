# AetherionOfficial — Datenschutzhinweise / Privacy Notice

**Vorläufige Fassung / Interim notice — Stand / Last updated: 18 October 2026**

AetherionOfficial is the project name used for the Aetherion DPS Meter and its Discord support community. For privacy questions or requests, contact **aetherionofficial@gmail.com**. Discord operates its platform under its own privacy policy: [discord.com/privacy](https://discord.com/privacy).

## Deutsch

### Wer ist verantwortlich?

Diese Datenschutzhinweise beschreiben die Verarbeitung im Zusammenhang mit Aetherion DPS Meter, dem Lizenzzugang und der Aetherion-Discord-Community. Als Projektname wird **AetherionOfficial** verwendet. Datenschutzanfragen kannst du an **aetherionofficial@gmail.com** richten. Discord verarbeitet Daten auf seiner Plattform zusätzlich in eigener Verantwortung.

### Welche Daten werden verarbeitet?

- **Discord-Community und Support:** Discord-Kennung, Profilname sowie Nachrichten und Anhänge, die du im Aetherion-Server oder in Support-/Lizenz-Tickets sendest. Das Team verwendet Ticketinhalte für Support, Zugangsverwaltung und die Untersuchung möglichen Missbrauchs. Bitte sende keine Bank- oder Kartendaten.
- **Anmeldung:** Aetherion verwendet Discord OAuth über Supabase Auth. Dabei werden die für Anmeldung und Kontozuordnung nötigen Kontodaten verarbeitet, insbesondere eine interne Nutzerkennung und, sofern vom Login bereitgestellt, Profilname, Avatar oder E-Mail-Adresse. Ein Sitzungserneuerungstoken wird lokal für dein Windows-Benutzerkonto mit DPAPI geschützt gespeichert.
- **Lizenz und Zugang:** Supabase verarbeitet Lizenztyp, Ausgabe-, Einlöse-, Ablauf- und Sperrzeitpunkte, die Zuordnung zum Auth-Konto und den Zugangsstatus. Lizenzschlüssel werden in der Datenbank als SHA-256-Hash gespeichert. Für die Installationsbindung wird eine zufällige Installationskennung dem Konto zugeordnet. Erkennt die automatische Prüfung eine andere Installation, kann der Zugang automatisch gesperrt werden. Bei einer vermuteten Fehlentscheidung kannst du über Discord eine manuelle Prüfung anfragen.
- **Lokaler App-Betrieb:** Die App erfasst auf deinem PC den Netzwerkverkehr des AION-2-Spiels, um Kämpfe auszuwerten. Einstellungen und Kampfverläufe werden standardmäßig lokal unter `%AppData%\\AionMeter` gespeichert. Optionale rohe Paketmitschnitte sind standardmäßig ausgeschaltet und werden bei Aktivierung lokal unter `%AppData%\\AionMeter\\captures` gespeichert. Diese Daten können Charakter- oder Spielernamen und Spieldaten enthalten. Die App lädt sie nicht zu Aetherion hoch. Dateien, die du an den Support sendest, kann das Team einsehen.
- **Kampfbuch und Ergebniskarten:** Notizen, Favoriten und Trainingsziele werden lokal in einer separaten Tagebuchdatei gespeichert. Neue Kampfdateien können zuordenbare Heilungsquellen und Empfängernamen enthalten. PNG-Ergebniskarten werden nur nach deiner Dateiauswahl lokal exportiert; Namen sind standardmäßig anonymisiert und im Streamer-Modus immer verborgen. Notizen, Ziele und Serverdaten werden nicht auf die Karte übernommen. Die App lädt Karten nicht automatisch zu Discord oder Aetherion hoch. Der Streamer-Modus verändert keine ursprünglich gespeicherten Kampf- oder Notizdaten.
- **Verbindungen zu anderen Diensten:** Die App verbindet sich mit Supabase für Anmeldung, Lizenzprüfung und Wartungsstatus, mit GitHub für Versionsabfragen sowie – wenn Bilddownloads aktiviert sind – mit dem offiziellen AION-2-CDN und MetaBot.GG für Bilder. Die optionale Karte lädt Questlog.gg über Microsoft WebView2, wenn du sie öffnest. Die jeweiligen Dienste können dabei technische Verbindungsdaten wie IP-Adresse, Zeitpunkt und angeforderte Ressource erhalten. Bilddownloads lassen sich in den Einstellungen deaktivieren.

### Zwecke und Rechtsgrundlagen

Die Verarbeitung dient der Anmeldung, Bereitstellung und Prüfung des Lizenzzugangs, dem Support und dem technischen Betrieb. Soweit erforderlich, stützt sie sich auf die Durchführung des Zugangsvertrags (Art. 6 Abs. 1 lit. b DSGVO) und auf berechtigte Interessen an Sicherheit, Support und Missbrauchsabwehr (Art. 6 Abs. 1 lit. f DSGVO). Die jeweils anwendbare Rechtsgrundlage hängt vom konkreten Verarbeitungsvorgang ab.

### Empfänger und internationale Verarbeitung

Daten erhalten nur die genannten Dienste und Personen, die sie für Betrieb oder Support benötigen: Discord, Supabase, GitHub und – abhängig von den verwendeten Funktionen – AION-CDN, MetaBot.GG sowie Questlog.gg/Microsoft WebView2. Anbieter können Daten außerhalb des Europäischen Wirtschaftsraums verarbeiten. Für die Verarbeitung durch die Anbieter gelten zusätzlich deren Datenschutzinformationen und Bedingungen.

### Speicherdauer

Lokale Einstellungen, Kampfverläufe und Mitschnitte bleiben auf deinem Gerät, bis du sie löschst; das Sitzungstoken kannst du durch Abmelden entfernen. Konto- und Lizenzdaten bleiben so lange gespeichert, wie das Konto und die Lizenzverwaltung aktiv sind und anschließend, soweit dies für die Prüfung früherer Lizenzen, Sicherheit, Support, Streitfälle oder gesetzliche Pflichten erforderlich ist. Support-Ticketdaten werden für die Bearbeitung des Anliegens und danach nur so lange vorgehalten, wie sie für Support, Sicherheit oder rechtliche Pflichten erforderlich sind. Die konkrete Dauer kann je nach Vorgang variieren.

### Deine Rechte und Kontakt

Je nach den gesetzlichen Voraussetzungen kannst du Auskunft, Berichtigung, Löschung oder Einschränkung der Verarbeitung verlangen, der Verarbeitung widersprechen und Datenübertragbarkeit geltend machen. Eine Einwilligung kannst du mit Wirkung für die Zukunft widerrufen. Kontaktiere **aetherionofficial@gmail.com**. Du kannst dich außerdem bei einer Datenschutzaufsichtsbehörde beschweren.

### Sicherheit und Änderungen

Zu den Schutzmaßnahmen gehören Zugriffsbeschränkungen, Row-Level Security in Supabase, gehashte Lizenzschlüssel und lokal mit DPAPI geschützte Sitzungstokens. Absolute Sicherheit kann nicht garantiert werden. Diese Hinweise werden aktualisiert, wenn sich die Verarbeitung oder die eingesetzten Dienste wesentlich ändern.

---

## English

### Who is responsible?

This notice describes processing related to the Aetherion DPS Meter, licence access and the Aetherion Discord support community. **AetherionOfficial** is the project name used here. Send privacy questions or requests to **aetherionofficial@gmail.com**. Discord separately controls data processed on its platform.

### What data is processed?

- **Discord community and support:** Discord user ID, profile name, messages and attachments you send in the Aetherion server or in support/licence tickets. The team uses ticket content to provide support, manage access and investigate possible abuse. Do not send bank or card details.
- **Sign-in:** Aetherion uses Discord OAuth through Supabase Auth. Data needed for sign-in and account matching is processed, including an internal user identifier and, if provided by the login, profile name, avatar or email address. A session refresh token is stored locally and protected for your Windows account with DPAPI.
- **Licence and access:** Supabase processes licence type, issue, redemption, expiry and revocation times, the link to the Auth account and access status. Licence keys are stored in the database as SHA-256 hashes. A random installation identifier is linked to the account to enforce the installation binding. If the automated check detects a different installation, access may be automatically blocked. You can request a manual review through Discord if you believe this was a mistake.
- **Local app operation:** The app captures AION 2 game network traffic on your PC to calculate combat statistics. Settings and fight histories are stored locally by default under `%AppData%\\AionMeter`. Optional raw packet captures are off by default and, if enabled, are stored locally under `%AppData%\\AionMeter\\captures`. These files may contain character/player names and game data. The app does not upload them to Aetherion. The team can access files you send to support.
- **Fight journal and result cards:** Notes, favorites and training goals are stored locally in a separate journal file. New fight records can include attributable healing sources and recipient names. PNG result cards are exported locally only after you choose a file location; names are anonymized by default and always hidden in streamer mode. Notes, goals and server data are not included on the card. The app does not automatically upload cards to Discord or Aetherion. Streamer mode does not change original fight or note data stored on disk.
- **Connections to other services:** The app connects to Supabase for sign-in, licence checks and maintenance status, GitHub for version checks and, when image downloads are enabled, the official AION 2 CDN and MetaBot.GG for images. The optional map loads Questlog.gg through Microsoft WebView2 when you open it. These services may receive technical connection data such as your IP address, time and requested resource. Image downloads can be disabled in settings.

### Purposes and legal bases

Processing supports sign-in, provision and checking of licence access, support and technical operation. Where applicable, it is based on performance of the access agreement (GDPR Article 6(1)(b)) and legitimate interests in security, support and abuse prevention (Article 6(1)(f)). The applicable legal basis depends on the specific processing activity.

### Recipients and international processing

Data is received only by the services listed above and people who need it to operate Aetherion or provide support: Discord, Supabase, GitHub and, depending on the features used, the AION CDN, MetaBot.GG and Questlog.gg/Microsoft WebView2. Providers may process data outside the European Economic Area. Their privacy notices and terms also apply to their processing.

### Retention

Local settings, fight histories and captures remain on your device until you delete them; you can remove the session token by signing out. Account and licence data are kept while the account and licence administration remain active, and afterwards where necessary to verify prior licences, maintain security, provide support, resolve disputes or meet legal obligations. Support ticket data is kept while handling the request and afterwards only as long as needed for support, security or legal obligations. The period may vary by case.

### Your rights and contact

Subject to applicable law, you may request access, correction, erasure or restriction, object to processing and exercise data portability rights. You may withdraw consent for future processing. Contact **aetherionofficial@gmail.com**. You may also complain to a data protection supervisory authority.

### Security and changes

Safeguards include access restrictions, Supabase Row Level Security, hashed licence keys and session refresh tokens protected locally with DPAPI. No method can guarantee absolute security. This notice will be updated if processing or services materially change.

---

This is an interim notice based on the current project code and documentation. The operator is presented under the **AetherionOfficial** project name and can be contacted at **aetherionofficial@gmail.com**. Review the notice and any applicable legal disclosure obligations for your situation.


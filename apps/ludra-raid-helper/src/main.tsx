import { StrictMode, useEffect, useMemo, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { createWorker } from 'tesseract.js';
import './style.css';

type Language = 'en' | 'de';
type Severity = 'info' | 'warning' | 'danger';
type Localized = { en: string; de: string };
type Mechanic = { name: Localized; at: number; severity: Severity; instruction: Localized; roles: Localized };
type Phase = { id: string; name: Localized; duration: number; summary: Localized; mechanics: Mechanic[] };
type RaidState = { startedAt: number | null; pausedAt: number | null; elapsedBeforePause: number };
type Difficulty = 'auto' | 'normal' | 'hard';

const phases: Phase[] = [
  { id: 'arrival', name: { en: 'Arrival', de: 'Ankunft' }, duration: 75, summary: { en: 'Keep the group spread and establish the safe side.', de: 'Gruppe verteilt halten und die sichere Seite bestimmen.' }, mechanics: [
    { name: { en: 'Frost Mark', de: 'Frostmal' }, at: 18, severity: 'warning', instruction: { en: 'Marked players move to the outer ring. Do not stack.', de: 'Markierte Spieler gehen an den äußeren Ring. Nicht stapeln.' }, roles: { en: 'All players', de: 'Alle Spieler' } },
    { name: { en: 'Mirror Barrage', de: 'Spiegelsalve' }, at: 48, severity: 'danger', instruction: { en: 'Face the safe symbols and dodge the mirrored line attack.', de: 'Zur sicheren Markierung drehen und dem gespiegelten Linienangriff ausweichen.' }, roles: { en: 'All players', de: 'Alle Spieler' } },
  ] },
  { id: 'veil', name: { en: 'Veil of Silence', de: 'Schleier der Stille' }, duration: 85, summary: { en: 'Interrupt the channel and rotate clockwise after each wave.', de: 'Zauber unterbrechen und nach jeder Welle im Uhrzeigersinn drehen.' }, mechanics: [
    { name: { en: 'Silent Channel', de: 'Stiller Kanal' }, at: 12, severity: 'danger', instruction: { en: 'Interrupt immediately. Save one interrupt for the second cast.', de: 'Sofort unterbrechen. Eine Unterbrechung für den zweiten Zauber aufheben.' }, roles: { en: 'DPS / Support', de: 'DPS / Support' } },
    { name: { en: 'Void Wave', de: 'Leerenwelle' }, at: 55, severity: 'warning', instruction: { en: 'Rotate clockwise behind the safe marker. Healers prepare a group heal.', de: 'Im Uhrzeigersinn hinter der sicheren Markierung drehen. Heiler bereiten Gruppenheilung vor.' }, roles: { en: 'Healer / All players', de: 'Heiler / Alle Spieler' } },
  ] },
  { id: 'collapse', name: { en: 'Aether Collapse', de: 'Ätherkollaps' }, duration: 90, summary: { en: 'Use the marked zones in order and keep the center clear.', de: 'Markierte Zonen der Reihe nach nutzen und die Mitte freihalten.' }, mechanics: [
    { name: { en: 'Collapse Zones', de: 'Kollapszonen' }, at: 20, severity: 'danger', instruction: { en: 'Take one zone per group, then return to the edge.', de: 'Pro Gruppe eine Zone übernehmen und danach an den Rand zurückkehren.' }, roles: { en: 'All players', de: 'Alle Spieler' } },
    { name: { en: 'Resonance', de: 'Resonanz' }, at: 64, severity: 'warning', instruction: { en: 'Tanks face the boss away. Support cleanse marked players.', de: 'Tanks drehen den Boss weg. Support entfernt Markierungen.' }, roles: { en: 'Tank / Support', de: 'Tank / Support' } },
  ] },
  { id: 'enrage', name: { en: 'Enrage', de: 'Wutanfall' }, duration: 60, summary: { en: 'Final burn. Ignore optional targets and use cooldowns.', de: 'Finale Schadensphase. Optionale Ziele ignorieren und Cooldowns nutzen.' }, mechanics: [
    { name: { en: 'Final Burn', de: 'Finaler Angriff' }, at: 8, severity: 'danger', instruction: { en: 'Use offensive cooldowns and finish the boss before the arena closes.', de: 'Offensive Cooldowns nutzen und den Boss vor dem Schließen der Arena besiegen.' }, roles: { en: 'All players', de: 'Alle Spieler' } },
  ] },
];

const storageKey = 'hithanya:ludra-standalone-state';
const languageKey = 'hithanya:ludra-language';
const emptyState: RaidState = { startedAt: null, pausedAt: null, elapsedBeforePause: 0 };
const readState = (): RaidState => { try { return { ...emptyState, ...JSON.parse(localStorage.getItem(storageKey) ?? '{}') } as RaidState; } catch { return emptyState; } };
const readLanguage = (): Language => localStorage.getItem(languageKey) === 'de' ? 'de' : 'en';
const format = (seconds: number) => { const safe = Math.max(0, Math.ceil(seconds)); return String(Math.floor(safe / 60)).padStart(2, '0') + ':' + String(safe % 60).padStart(2, '0'); };
const copy = (value: Localized, language: Language) => value[language];

function App() {
  const [state, setState] = useState<RaidState>(() => readState());
  const [language, setLanguage] = useState<Language>(() => readLanguage());
  const [ttsEnabled, setTtsEnabled] = useState(true);
  const [voices, setVoices] = useState<SpeechSynthesisVoice[]>([]);
  const [voiceName, setVoiceName] = useState('');
  const [now, setNow] = useState(Date.now());
  const [manualPhase, setManualPhase] = useState(0);
  const [message, setMessage] = useState('');
  const [difficulty, setDifficulty] = useState<Difficulty>('auto');
  const [detectedMode, setDetectedMode] = useState<'normal' | 'hard' | null>(null);
  const [scanStatus, setScanStatus] = useState('');
  const channelRef = useRef<BroadcastChannel | null>(null);
  const spokenRef = useRef('');
  const ocrWorker = useRef<{ recognize: (image: string) => Promise<{ data: { text: string } }>; terminate: () => Promise<unknown> } | null>(null);

  const ui = language === 'de' ? {
    subtitle: 'Echtzeit-Mechanikassistent für Aion-2-Raids', ready: 'BEREIT', live: 'LIVE-RAID', next: 'NÄCHSTE MECHANIK', until: 'bis zur nächsten Mechanik', responsible: 'VERANTWORTLICH', raidTimeline: 'RAID-ZEITPLAN', liveInstructions: 'LIVE-ANWEISUNGEN', mechanics: 'Mechaniken', synced: 'Über offene Helfer-Tabs synchronisiert', start: 'Start / Fortsetzen', restart: 'Timer neu starten', pause: 'Pause', reset: 'Zurücksetzen', ttsTitle: 'Sprachansagen', ttsOn: 'TTS aktiv', ttsOff: 'TTS aus', voice: 'Stimme', test: 'Stimme testen', selectVoice: 'Automatische Stimme', language: 'Sprache', footer: 'Eigenständiger Raid Helper · Mechaniken sind erweiterbar', started: 'Live-Timer gestartet.', paused: 'Timer pausiert.', resetDone: 'Raid zurückgesetzt.', changed: 'Phase gewechselt zu', noMechanic: 'Phasenwechsel', raidLead: 'Raidleitung', english: 'English', german: 'Deutsch', chooseVoice: 'Stimme auswählen', browserNote: 'Der Browser benötigt einmalig eine Benutzeraktion für Sprachansagen.', testText: 'Ludra Raid Helper. Sprachansagen sind aktiviert.', mode: 'Ludra-Modus', auto: 'Automatisch erkennen', normal: 'Normalmode', hard: 'Hardmode', detected: 'Erkannt', scanning: 'Bildschirm wird geprüft …', scanUnavailable: 'Automatische Erkennung ist nur in der Desktop-App verfügbar.'
  } : {
    subtitle: 'Real-time mechanics assistant for Aion 2 raids', ready: 'READY', live: 'LIVE RAID', next: 'NEXT MECHANIC', until: 'until next mechanic', responsible: 'RESPONSIBLE', raidTimeline: 'RAID TIMELINE', liveInstructions: 'LIVE INSTRUCTIONS', mechanics: 'mechanics', synced: 'Synced across open helper tabs', start: 'Start / Resume', restart: 'Restart timer', pause: 'Pause', reset: 'Reset', ttsTitle: 'Voice announcements', ttsOn: 'TTS enabled', ttsOff: 'TTS off', voice: 'Voice', test: 'Test voice', selectVoice: 'Automatic voice', language: 'Language', footer: 'Standalone raid helper · mechanics are configurable', started: 'Live timer started.', paused: 'Timer paused.', resetDone: 'Raid reset.', changed: 'Phase changed to', noMechanic: 'Phase transition', raidLead: 'Raid lead', english: 'English', german: 'Deutsch', chooseVoice: 'Choose voice', browserNote: 'The browser needs one user action before voice announcements can play.', testText: 'Ludra Raid Helper. Voice announcements are enabled.', mode: 'Ludra mode', auto: 'Automatic detection', normal: 'Normal mode', hard: 'Hardmode', detected: 'Detected', scanning: 'Scanning screen…', scanUnavailable: 'Automatic detection is only available in the desktop app.'
  };

  useEffect(() => { const timer = window.setInterval(() => setNow(Date.now()), 250); const channel = typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel('hithanya-ludra-standalone'); channelRef.current = channel; const receive = (event: MessageEvent<RaidState>) => setState(event.data); channel?.addEventListener('message', receive); return () => { window.clearInterval(timer); channel?.removeEventListener('message', receive); channel?.close(); channelRef.current = null; }; }, []);
  useEffect(() => { if (!('speechSynthesis' in window)) return; const loadVoices = () => setVoices(window.speechSynthesis.getVoices()); loadVoices(); window.speechSynthesis.addEventListener('voiceschanged', loadVoices); return () => window.speechSynthesis.removeEventListener('voiceschanged', loadVoices); }, []);
  useEffect(() => { localStorage.setItem(languageKey, language); spokenRef.current = ''; }, [language]);

  const update = (next: RaidState) => { setState(next); localStorage.setItem(storageKey, JSON.stringify(next)); channelRef.current?.postMessage(next); };
  const elapsed = state.startedAt === null ? state.elapsedBeforePause : state.pausedAt === null ? state.elapsedBeforePause + (now - state.startedAt) / 1000 : state.elapsedBeforePause + (state.pausedAt - state.startedAt) / 1000;
  const total = phases.reduce((sum, item) => sum + item.duration, 0);
  const phaseIndex = useMemo(() => { if (state.startedAt === null && state.elapsedBeforePause === 0) return manualPhase; let remaining = elapsed; for (let index = 0; index < phases.length - 1; index += 1) { const current = phases[index]!; if (remaining < current.duration) return index; remaining -= current.duration; } return phases.length - 1; }, [elapsed, manualPhase, state.elapsedBeforePause, state.startedAt]);
  const phase = phases[phaseIndex] ?? phases[0]!;
  const offset = phases.slice(0, phaseIndex).reduce((sum, item) => sum + item.duration, 0);
  const phaseElapsed = Math.max(0, elapsed - offset);
  const next = phase.mechanics.find((item) => item.at >= phaseElapsed) ?? phases[phaseIndex + 1]?.mechanics[0];
  const nextIn = next ? Math.max(0, next.at >= phaseElapsed ? next.at - phaseElapsed : phase.duration - phaseElapsed + next.at) : 0;
  const running = state.startedAt !== null && state.pausedAt === null;
  const filteredVoices = voices.filter((voice) => voice.lang.toLowerCase().startsWith(language === 'de' ? 'de' : 'en'));

  const speak = (text: string) => { if (!ttsEnabled || !('speechSynthesis' in window)) return; window.speechSynthesis.cancel(); const utterance = new SpeechSynthesisUtterance(text); utterance.lang = language === 'de' ? 'de-DE' : 'en-US'; utterance.rate = language === 'de' ? 0.9 : 0.96; const selected = voices.find((voice) => voice.name === voiceName) ?? filteredVoices[0] ?? voices[0]; if (selected) utterance.voice = selected; window.speechSynthesis.speak(utterance); };
  useEffect(() => { if (!running || !next) return; const key = language + ':' + phase.id + ':' + next.name.en; if (spokenRef.current === key || nextIn > 2) return; spokenRef.current = key; speak(copy(next.name, language) + '. ' + copy(next.instruction, language)); }, [language, next, nextIn, phase.id, running]);

  const changePhase = (index: number) => { const selected = phases[index]; if (!selected) return; setManualPhase(index); update({ startedAt: null, pausedAt: null, elapsedBeforePause: phases.slice(0, index).reduce((sum, item) => sum + item.duration, 0) }); setMessage(ui.changed + ' ' + copy(selected.name, language) + '.'); };
  const start = () => { update({ startedAt: Date.now(), pausedAt: null, elapsedBeforePause: state.startedAt === null ? state.elapsedBeforePause : elapsed }); setMessage(ui.started); };
  const pause = () => { if (running) { update({ startedAt: state.startedAt, pausedAt: Date.now(), elapsedBeforePause: state.elapsedBeforePause }); setMessage(ui.paused); } };
  const reset = () => { update(emptyState); setManualPhase(0); setMessage(ui.resetDone); spokenRef.current = ''; };
  const setAppLanguage = (value: Language) => { setLanguage(value); setVoiceName(''); };
  const scanScreen = async () => { if (!window.raidHelperDesktop?.captureScreen) { setScanStatus(ui.scanUnavailable); return; } setScanStatus(ui.scanning); try { const worker = ocrWorker.current ?? await createWorker(language === 'de' ? 'deu' : 'eng'); ocrWorker.current = worker; const image = await window.raidHelperDesktop.captureScreen(); if (!image) throw new Error('No screen image'); const result = await worker.recognize(image); const text = result.data.text.toLowerCase().replace(/\s+/g, ' '); const found = /hard ?mode|hardmode/.test(text) ? 'hard' : /normal ?mode|normalmode/.test(text) ? 'normal' : null; if (found) setDetectedMode(found); setScanStatus(found ? ui.detected + ': ' + (found === 'hard' ? ui.hard : ui.normal) : ''); } catch { setScanStatus('OCR scan failed. You can choose the mode manually.'); } };
  useEffect(() => { if (difficulty !== 'auto') return; void scanScreen(); const timer = window.setInterval(() => void scanScreen(), 10000); return () => window.clearInterval(timer); }, [difficulty, language]);
  useEffect(() => () => { void ocrWorker.current?.terminate(); }, []);

  return <main className="app"><header><div className="brand"><div className="brand-mark">L</div><div><h1>Ludra Raid Helper</h1><p className="sub">{ui.subtitle}</p></div></div><div className="header-actions"><label className="language-picker"><span>{ui.language}</span><select value={language} onChange={(event) => setAppLanguage(event.target.value as Language)}><option value="en">{ui.english}</option><option value="de">{ui.german}</option></select></label><div className={'status ' + (running ? 'live' : '')}><i />{running ? ui.live : ui.ready}</div></div></header><section className="mode-panel"><label><span>{ui.mode}</span><select value={difficulty} onChange={(event) => setDifficulty(event.target.value as Difficulty)}><option value="auto">{ui.auto}</option><option value="normal">{ui.normal}</option><option value="hard">{ui.hard}</option></select></label><div className="mode-result">{difficulty === "auto" && detectedMode ? ui.detected + ": " + (detectedMode === "hard" ? ui.hard : ui.normal) : difficulty === "auto" ? scanStatus : difficulty === "hard" ? ui.hard : ui.normal}</div>{difficulty === "auto" && <button onClick={() => void scanScreen()}>{ui.test}</button>}</section><section className="tts-panel"><div><span className="label">{ui.ttsTitle.toUpperCase()}</span><strong>{ttsEnabled ? ui.ttsOn : ui.ttsOff}</strong><p>{ui.browserNote}</p></div><label className="switch"><input type="checkbox" checked={ttsEnabled} onChange={(event) => setTtsEnabled(event.target.checked)} /><span /></label><label className="voice-picker"><span>{ui.voice}</span><select value={voiceName} onChange={(event) => setVoiceName(event.target.value)}><option value="">{filteredVoices.length ? ui.selectVoice : ui.chooseVoice}</option>{filteredVoices.map((voice) => <option value={voice.name} key={voice.name + '-' + voice.lang}>{voice.name} ({voice.lang})</option>)}</select></label><button className="test-voice" onClick={() => speak(ui.testText)}>{ui.test}</button></section><section className="top-grid"><article className={'focus ' + (next?.severity ?? 'info')}><span className="label">{ui.next}</span><h2>{next ? copy(next.name, language) : ui.noMechanic}</h2><p>{next ? copy(next.instruction, language) : copy(phase.summary, language)}</p><div className="timer">{format(nextIn)}<small>{ui.until}</small></div><div className="responsible"><span>{ui.responsible}</span><b>{next ? copy(next.roles, language) : ui.raidLead}</b></div></article><article className="timeline"><div className="section-head"><div><span className="label">{ui.raidTimeline}</span><h2>{copy(phase.name, language)}</h2></div><b>{format(Math.max(0, total - elapsed))}</b></div><div className="bar"><i style={{ width: Math.min(100, elapsed / total * 100) + '%' }} /></div><div className="phases">{phases.map((item, index) => <button className={index === phaseIndex ? 'selected' : ''} onClick={() => changePhase(index)} key={item.id}><em>{index + 1}</em><span>{copy(item.name, language)}</span><small>{format(item.duration)}</small></button>)}</div></article></section><div className="controls"><button className="primary" onClick={start}>{running ? ui.restart : ui.start}</button><button onClick={pause} disabled={!running}>{ui.pause}</button><button onClick={reset}>{ui.reset}</button><span>{ui.synced}</span></div><section className="mechanics"><div className="section-head"><div><span className="label">{ui.liveInstructions}</span><h2>{copy(phase.name, language)} {ui.mechanics}</h2><p>{copy(phase.summary, language)}</p></div><b>{format(phaseElapsed)} / {format(phase.duration)}</b></div><div className="mechanic-list">{phase.mechanics.map((item) => <article className={'mechanic ' + item.severity} key={item.name.en}><i /><div><h3>{copy(item.name, language)}</h3><p>{copy(item.instruction, language)}</p><small>{copy(item.roles, language)}</small></div><time>{format(item.at)}</time></article>)}</div></section>{message && <p className="message">{message}</p>}<footer>{ui.footer}</footer></main>;
}

createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);



# Entscheidungs-Delta zur Übergabe 98–113 – nicht selbst freigegeben

Nur Manuel entscheidet. Nichts davon ist in `requirements.yaml` oder `docs/spec/decisions.yaml` eingetragen.

## A. Register
| # | Delta | Vorschlag | Folge |
|---|---|---|---|
| A1 | IMP-98 … IMP-113 fehlen in `decisions.yaml` und `requirements.yaml` (beide enden bei IMP-97) | 16 Einträge `decision: APPROVED`, `implementation_status: NOT_STARTED`/`IN_PROGRESS` je Befund, `test_status: NOT_RUN`, Quelle = Übergabe-MD + SHA | `DecisionRegisterTests` prüft heute „13–97“ und muss mitgezogen werden |
| A2 | IMP-95/96: Funktionslisten fehlen (Repo und Paket) | als `BLOCKED_SOURCE` führen, nicht erraten | Abgrenzung 95 ↔ IMP-55 ↔ 102 ARC-21…30 offen |
| A3 | IMP-114 ff. | weiter pausiert | – |

## B. Widersprüche zwischen den Spezifikationen
| # | Widerspruch | Bitte entscheiden |
|---|---|---|
| B1 | 104 nennt 105 „OFFEN“, 105 selbst „JA am 27.10.2026“ | 105 = JA bestätigen? (Paket-Manifest sagt 98–113 APPROVED_SPEC) |
| B2 | 109 Zeile 640: 110 „separat vorgeschlagen, nicht Teil dieser Freigabe“; 110 selbst „JA / APPROVED_SPEC“ | 110 = JA bestätigen? |
| B3 | 113 überschneidet sich mit **IMP-65** „VR, AR & Mixed Reality Studio“ (APPROVED, ohne Funktionsliste) | 113 ersetzt/konkretisiert IMP-65 (ein XR-Studio, kein zweites)? |
| B4 | 113 verlangt blaue Optik ohne Gold; `CLAUDE.md`: Gold-Blau ist Leitfarbe (Logo C) | Gold-Blau beibehalten? |
| B5 | 111-01…10 überschneiden sich mit **IMP-17** (IN_PROGRESS); 111-28 und 111-41 beschreiben dieselbe Plattformmatrix | als eine Funktion führen? |

## C. Konflikte mit dem heutigen Code oder mit Sperren
| # | Konflikt | Vorschlag |
|---|---|---|
| C1 | 98 Zero-Config verlangt mitgelieferte Sender; Code und Capability-Matrix sagen ausdrücklich „Trendo bringt keine Sender mit“ | Nur frei und legal empfangbare Quellen (z. B. öffentlich-rechtliche Livestreams/Mediatheken mit offizieller Adresse) als abschaltbare Voreinstellung – braucht deine Freigabe und eine Rechtsprüfung je Quelle |
| C2 | 105-05/105-22 verlangen Bestandsabgang erst bei Lieferung und Erlös erst bei Zahlung; der bestehende Test `Teil4BusinessTests` „Online verkaufen F1/F2/U1/F3“ prüft das Gegenteil | Verhalten ändern und Test bewusst anpassen? |
| C3 | IMP-86 NEIN: CRM-/Ticket-Bausteine aus BUS-019/023 (`BizContact`-Pipeline, `Ticket`/`Sla`/`Faq` in `BusinessOps.cs`) existieren; 105-11/-39/-44/-45 und 106 CNT-22/CNT-28 würden sie ausbauen | Diese Funktionen nur ohne CRM-/Service-Desk-Ausbau umsetzen; sonst streichen |
| C4 | 108-49: Spec verlangt Diff-Vorschlag, heute ersetzt `TextAiRewrite` sofort | auf Vorschlag umstellen (Verhaltensänderung) |
| C5 | 108-31…40 (Lernen) und 100 O100-18 dürfen nicht zum Language Studio 2.0 / SPEC-07 werden | Grenze: nur vorhandene Übersetzungsfunktion nutzen |
| C6 | 99 „Autonomous Task Director“, 103-04, 104-36, 110: Gefahr eines zweiten Agenten-/Scheduler-Kerns | nur `WorkflowEngine`, `AgentCenter`, `tools/trendo_ops.py`, `ApprovalGate` erweitern |
| C7 | 110: Ersatz-KI (T-0225) springt ein, wenn die lokale KI schweigt | Im Modus „nur lokal“ hart abschaltbar machen (kein stiller Cloud-Fallback) |
| C8 | 103-20/-28/-29/-45/-50 beschreiben AION/GENESIS-Spielmechanik | in Trendo nur Werkzeug-Schemata und Prüfer, keine Spielinhalte |
| C9 | 102/SIM-48/50 berühren **IMP-97** (OFFEN) | bis zur Entscheidung ausklammern |
| C10 | 112 + GRUNDSATZ-01: Erweiterungskataloge von beliebigen GitHub-Konten | nur freigegebene Quellen zulassen? |

## D. Unveränderlich (bestätigt, nicht berührt)
IMP-86 REJECTED · IMP-97 UNDECIDED · frühe 23/25/34 NEIN · 35 OFFEN · Language Studio 2.0 NEIN · SPEC-07 abgelehnt · 120-Stunden-Forensikregel (`ForensicsLab.MaxLife`) · 114+ pausiert.

## E. Entscheidungen von Manuel am 31.10.2026 (im Chat)
| # | Frage | Entscheidung | Umgesetzt |
|---|---|---|---|
| A1 | 98–113 ins Register | **JA** – als APPROVED, Status ehrlich | `decisions.yaml`, `requirements.yaml`, `IMPROVEMENTS.csv` (IMP-98…113); `DecisionRegisterTests` prüft 13–113 |
| A2 | 95/96 ohne Funktionsliste | **Blockiert**, bis Unterlagen da sind – nichts erraten | Notiz `BLOCKED_SOURCE` an IMP-95/96 |
| B1/B2 | 105 und 110 freigegeben? | **Beide JA** | Register |
| B3 | 113 vs. IMP-65 | **113 ersetzt/konkretisiert 65** – ein XR-Studio | Notiz an IMP-65 und IMP-113 |
| B4 | Farbe in 113 | **Gold-Blau bleibt** für ganz Trendo | Notiz an IMP-113 |
| C1 | Zero-Config-Sender | **Nur legale, frei empfangbare Quellen**, abschaltbar, Rechte je Quelle geprüft | Notiz an IMP-98 (noch nicht gebaut) |
| C2 | Shop: Bestand/Erlös | **Wie 105** (Bestand bei Lieferung, Erlös bei Zahlung); bestehender Test wird bewusst angepasst | Notiz an IMP-105 (noch nicht gebaut) |
| C3 | CRM-Bausteine | **Nur ohne CRM-Ausbau** (keine Pipeline, kein Service-Desk, keine SLA) | Notiz an IMP-105/106 |
| C4 | KI-Umschreiben | **Vorschlag zum Annehmen** statt sofort ersetzen | Notiz an IMP-108 (noch nicht gebaut) |
| C7 | Ersatz-KI im Modus „nur lokal“ | **Bleibt wie heute** – darf einspringen | Notiz an IMP-110 |
| C10 | Erweiterungsquellen | **Jedes GitHub-Konto**, mit Rückfrage und Hinweis „ungeprüft“ (Stand T-0241) | Notiz an IMP-112 |

Unverändert gesperrt: IMP-86 REJECTED, IMP-97 UNDECIDED, 23/25/34 NEIN, 35 OFFEN, Language Studio 2.0 NEIN, SPEC-07 abgelehnt, 114+ pausiert, 120-Stunden-Forensikregel.

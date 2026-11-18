# MindSpace AI

**Brainstorm together in 3D. AI groups the ideas, your team votes and decides, and you leave with an action plan.**

MindSpace AI is a collaborative brainstorming room for Meta Quest, built in Unity. Ideas become floating 3D cards that everyone in the room can grab and move. One button asks an AI model to group the cards into themes, shortlist the best three and draft a five-step plan. The team votes, presses Decide, and exports a Markdown report.

> **Status:** hackathon prototype. The server was tested with scripted requests, and the Unity project was opened and run in Play mode in the Unity 6.6 Editor (the panel renders and connects to the server). It has **not** been tested on a Quest headset yet.

## Features

- **Rooms by code:** everyone using the same room code sees the same board.
- **Idea cards in 3D:** grab and move them with Quest controllers, or with the mouse in the Editor.
- **AI organizing:** groups ideas into themes, picks a top 3 with reasons, drafts a 5-step plan. The model output is validated against the real card IDs and never rewrites idea text.
- **Works without AI:** if there is no API key or the call fails, a keyword-based grouping runs instead.
- **Vote and decide:** one vote per person per card (toggle), and a shared "decision" card.
- **Export:** Markdown report with the decision, ideas by theme, votes, shortlist and plan.

## How it works

```
Unity app (Quest or Editor)  <-- HTTP polling, JSON -->  Node.js server  --> Anthropic API (optional)
 - XR Interaction Toolkit                                 - rooms in memory
 - world-space control panel                              - cards, votes, decision
 - grabbable 3D cards                                     - AI grouping + validation
                                                          - Markdown export
```

The Unity client polls the server about four times a second and sends card positions while you drag. The API key lives only on the server.

## Repository layout

If you unzip `MindSpaceUnity.zip`, the files may sit one folder deeper (`MindSpaceUnity/MindSpaceUnity/...`). The project root is the folder that contains `Assets`, `Packages` and `server`.

```
server/server.js                      Node relay + AI backend (no dependencies)
Assets/MindSpace/Scripts/
  MindSpaceApp.cs                     networking, control panel, mouse input
  CardView.cs                         grabbable card
  Models.cs                           JSON data classes
  Editor/MindSpaceSetup.cs            menu: MindSpace > Setup Scene
Packages/manifest.json                package versions used
```

## Requirements

- **Unity 6** (developed on 6000.6.9f1), Universal 3D (URP) project
- Packages: XR Interaction Toolkit, XR Plugin Management, OpenXR Plugin, Input System, TextMesh Pro (included with Unity UI)
- **Node.js 18 or newer**
- Optional: an Anthropic API key for AI-based grouping
- For headset builds: Meta Quest with developer mode, Android build support in Unity

## Quick start

### 1. Run the server

```bash
cd server
node server.js
```

You should see `MindSpace server on :8787`. To use the AI model, set a key first:

```bash
# macOS / Linux
export ANTHROPIC_API_KEY=your-key
# Windows (cmd)
set ANTHROPIC_API_KEY=your-key
node server.js
```

Optional variables: `PORT` (default `8787`) and `ANTHROPIC_MODEL` (choose which model to use).

Check it in a browser at `http://localhost:8787/api/room/DEMO`. A `404` at `http://localhost:8787/` is normal, because the server only answers `/api/room/...`.

### 2. Set up Unity

1. Create a **Universal 3D** project in Unity 6, or open an existing URP project.
2. Copy `Assets/MindSpace` into the project's `Assets` folder (use File Explorer or Finder).
3. Open **Window > Package Management > Package Manager**, switch to **Unity Registry**, and install **XR Interaction Toolkit**, **XR Plugin Management**, **OpenXR Plugin** and **Input System**. Accept the restart prompt. Import **TMP Essentials** if asked.
4. Set **Edit > Project Settings > Player > Other Settings > Active Input Handling** to *Input System Package (New)* or *Both*.
5. Create a scene inside `Assets` (right-click in the Project panel: **Create > Scene**, not Scene Template) and open it.
6. Run **MindSpace > Setup Scene**. It adds an XR Origin and a `MindSpace` object and allows HTTP.
7. Select the `MindSpace` object and set **Server Url** to `http://localhost:8787` (or `http://<your-laptop-ip>:8787` for a headset).
8. Press **Play** and open the **Game** tab. Set **Scale** to 1x. Hover over it and press **Shift+Space** to maximize.

### 3. Use it

1. Click **Load demo ideas** (a campus food-waste scenario).
2. Click **Organize with AI** and watch the cards move into themed columns.
3. Drag a card, select it, then **Vote** or **Decide**.
4. Click **Export plan**. The file is saved on the device, and the status line shows the URL to open it on a laptop.

### Headset build

In **Project Settings > XR Plug-in Management**, open the Android tab and enable **OpenXR** with **Meta Quest Support**. Under **Player > Android**, use IL2CPP and ARM64, and set **Allow downloads over HTTP** to *Always allowed* (the **MindSpace > Setup Scene** menu does this). Use your laptop's LAN IP in **Server Url**. The headset and laptop must be on the same network. Then **Build and Run**.

## Controls

| Action | Mouse (Editor) | Quest controllers |
|---|---|---|
| Move a card | Left-drag on the card | Point the ray at a card, hold the trigger, move |
| Select a card | Click it | Grab it |
| Press a panel button | Click | Point the ray at it and press the trigger |
| Type an idea | Click the field and type | System keyboard (if available) |

Panel buttons: **Join** (room code), **Add**, **Organize with AI**, **Load demo ideas**, **Vote**, **Decide**, **Export plan**, **Clear room**.

## Configuration

Set these on the `MindSpace` object (the `MindSpaceApp` component):

| Field | Default | Meaning |
|---|---|---|
| Server Url | `http://192.168.1.20:8787` | Address of the Node server. Use `http://localhost:8787` in the Editor |
| Room Code | `DEMO` | Room to join at start |
| Board Center | `(0, 1.5, 2)` | Where the card board sits in the world |
| Board Scale | `0.4` | Metres per board unit |

## Server API

All routes are under `/api/room/{code}`. The room is created on first use.

| Method | Path | Body | Purpose |
|---|---|---|---|
| GET | `/` | query `since=<version>` | Current room state (cheap when unchanged) |
| POST | `/card` | `{id?, text, author, x, y, z}` | Create a card, or update its position |
| POST | `/vote` | `{id, voter}` | Toggle a vote |
| POST | `/decide` | `{id}` | Set the decision card |
| POST | `/organize` | none | Run AI grouping, shortlist and plan |
| POST | `/clear` | none | Remove all cards |
| GET | `/export` | none | Markdown report |

## Limitations

- Not tested on a headset yet.
- No login or permissions, so anyone with a room code can edit the room.
- Rooms live in server memory and disappear on restart.
- HTTP polling (about 4 Hz) instead of WebSockets. Expect small delays.
- Typing in VR depends on the system keyboard being available. Use **Load demo ideas** if it isn't.
- On a PC, the control panel appears as a floating 3D panel, which can look small or soft in the Game view. Maximize the Game view and use Scale 1x.
- Not built yet: avatars, voice, hand tracking.

## Troubleshooting

| Problem | Fix |
|---|---|
| Red error about `XRGrabInteractable` | The XR Interaction Toolkit package is not installed yet |
| `'node' is not recognized` | Install Node.js LTS and restart the PC |
| Panel says "Offline" | The server is not running, or **Server Url** is wrong |
| "Scene needs to be saved inside the Assets folder" | Create the scene from the Project panel instead of the Save dialog |
| Yellow warning about a Unicode character | Harmless: some symbols are missing from the default font |
| Cards or panel look cut off | Set the Game view **Scale** to 1x and the aspect to Free Aspect |

## Roadmap

Avatars and spatial voice, hand tracking, WebSocket sync, facilitator role and room permissions, persistent storage, and shareable reports.

## Web demo

A browser version of the same loop (shared cards, live presence, AI grouping, export) is available here: https://claude.ai/artifact/9yQEgXyZ7BTwYekPE4UNnZ

## License

MIT. See [LICENSE](LICENSE). Unity, XR Interaction Toolkit, TextMesh Pro and other packages keep their own licenses.

AI suggestions are advisory. The team makes the final decision.

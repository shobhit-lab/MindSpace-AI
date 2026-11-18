# MindSpace AI — Unity (VR) edition

Quest-ready collaborative brainstorming: grab idea cards in 3D, AI clusters them, vote, decide, export.
Unity 6 (6000.0 LTS) · URP · XR Interaction Toolkit 3 · OpenXR · Node relay server.
**Not compiled or run by the author — expect small version fixes on first import.**

## 1. Run the server (laptop)
    cd server
    # optional, enables Claude (otherwise a local keyword fallback is used)
    export ANTHROPIC_API_KEY=sk-ant-...
    node server.js            # listens on :8787
Find your laptop LAN IP (e.g. 192.168.1.20). Headset and laptop must share Wi-Fi.

## 2. Unity project
1. Unity Hub → New project → **Universal 3D (Unity 6)**. Close it, copy this repo's `Assets/` and `Packages/manifest.json` over the new project, reopen.
2. Import **TMP Essentials** when prompted. Project Settings → Player → Active Input Handling = **Input System Package (New)** (restart editor).
3. Project Settings → XR Plug-in Management → **Android tab: OpenXR** + feature group **Meta Quest Support**; Interaction Profiles: Oculus Touch. (Windows tab OpenXR too for editor testing.)
4. Player → Android: Minimum API 29+, IL2CPP, ARM64, **Other Settings → Allow downloads over HTTP = Always allowed**. Build Settings → switch to Android.
5. Open any scene and run the menu **MindSpace → Setup Scene**. It removes the default camera, adds XR Origin (VR), creates the `MindSpace` object with **MindSpaceApp**, and allows HTTP. (Manual alternative: `GameObject > XR > XR Origin (VR)` plus an empty object with MindSpaceApp.)
6. Select the `MindSpace` object and set **Server Url** to `http://<laptop-ip>:8787` (`http://localhost:8787` for Editor testing).
7. Build & Run to Quest (or press Play: mouse drags cards, clicks the panel).

## Using it
Panel on the left: Join room code → Load demo ideas → Organize with AI → grab/drag cards with controllers
(ray + grip/trigger) → select a card, Vote → Decide → Export (file saved on device + URL for laptop).
Several headsets/Editor instances in the same room code sync via the server (≈4 Hz polling).

## Layout
- `server/server.js` — rooms, sync, Claude call (schema-validated) + local fallback, Markdown export
- `Assets/MindSpace/Scripts/MindSpaceApp.cs` — networking, world-space UI, mouse/XR input
- `CardView.cs` — grabbable card · `Models.cs` — JSON DTOs

## Known gaps vs the PRD
No avatars/voice/hand tracking, no auth (room code only), polling instead of WebSockets, server state is in memory.

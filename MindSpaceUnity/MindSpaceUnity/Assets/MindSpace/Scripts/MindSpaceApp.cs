using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MindSpace
{
    /// Drop on ONE empty GameObject. Builds the board, the control panel and the networking at runtime.
    public class MindSpaceApp : MonoBehaviour
    {
        [Tooltip("LAN address of the Node server, e.g. http://192.168.1.20:8787")]
        public string serverUrl = "http://192.168.1.20:8787";
        public string roomCode = "DEMO";
        public Vector3 boardCenter = new(0, 1.5f, 2f);
        public float boardScale = .4f;

        readonly Dictionary<string, CardView> cards = new();
        Transform board; Camera cam; CardView selected; int version = -1; string decisionId = "";
        string me; string myName; float nextPush;
        TMP_InputField ideaInput, roomInput; TMP_Text status, info;
        CardView mouseDrag; Vector3 mouseOffset;

        Vector3 ToWorld(Vector3 b) => boardCenter + b * boardScale;
        Vector3 ToBoard(Vector3 w) => (w - boardCenter) / boardScale;
        float holdUntil;
        public Vector3 HeadPosition => cam ? cam.transform.position : Vector3.zero;
        public bool IsMouseDragging(CardView c) => mouseDrag == c;

        void Start()
        {
            cam = Camera.main;
            me = SystemInfo.deviceUniqueIdentifier; myName = SystemInfo.deviceName.Length > 12 ? SystemInfo.deviceName[..12] : SystemInfo.deviceName;
            board = new GameObject("Board").transform; board.position = boardCenter;
            BuildUi();
            StartCoroutine(Poll());
        }

        // ---------- networking ----------
        string Url(string a = "") => $"{serverUrl}/api/room/{roomCode}{(a == "" ? "" : "/" + a)}";
        IEnumerator Poll()
        {
            while (true)
            {
                using var r = UnityWebRequest.Get(Url() + "?since=" + version);
                r.timeout = 5; yield return r.SendWebRequest();
                if (r.result == UnityWebRequest.Result.Success) { Say($"Room {roomCode} · {cards.Count} ideas · online"); Ingest(r.downloadHandler.text); }
                else Say("Offline — check server URL: " + serverUrl);
                yield return new WaitForSeconds(.25f);
            }
        }
        IEnumerator Post(string action, object payload = null)
        {
            var json = payload == null ? "{}" : JsonUtility.ToJson(payload);
            using var r = new UnityWebRequest(Url(action), "POST") { uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)), downloadHandler = new DownloadHandlerBuffer() };
            r.SetRequestHeader("Content-Type", "application/json"); r.timeout = 20;
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success) Ingest(r.downloadHandler.text); else Say("Request failed: " + r.error);
        }
        void Ingest(string json)
        {
            var s = JsonUtility.FromJson<StateDto>(json);
            if (s == null || s.unchanged) return;
            version = s.version; decisionId = s.decision ?? "";
            var seen = new HashSet<string>();
            foreach (var d in s.cards)
            {
                seen.Add(d.id);
                if (!cards.TryGetValue(d.id, out var v)) { v = CardView.Create(this, board, d); cards[d.id] = v; v.transform.position = ToWorld(new Vector3(d.x, d.y, d.z)); v.Target = v.transform.position; }
                v.Decided = d.id == decisionId; v.Selected = v == selected; v.Apply(d);
                if (!v.Grab.isSelected && mouseDrag != v) v.Target = ToWorld(new Vector3(d.x, d.y, d.z));
            }
            foreach (var id in new List<string>(cards.Keys)) if (!seen.Contains(id)) { Destroy(cards[id].gameObject); cards.Remove(id); }
            var sb = new StringBuilder();
            if (decisionId != "" && cards.TryGetValue(decisionId, out var dc)) sb.AppendLine($"★ DECISION: {dc.Data.text}\n");
            if (s.ai != null && s.ai.top.Count > 0)
            {
                sb.AppendLine($"AI shortlist ({s.ai.source}):"); foreach (var t in s.ai.top) sb.AppendLine($"• {t.text} — {t.why}");
                sb.AppendLine("\nPlan:"); for (int i = 0; i < s.ai.plan.Count; i++) sb.AppendLine($"{i + 1}. {s.ai.plan[i]}");
            }
            info.text = sb.Length > 0 ? sb.ToString() : "Add ideas, then press Organize.";
        }
        void PushPosition(CardView c)
        {
            var p = ToBoard(c.transform.position);
            StartCoroutine(Post("card", new CardPost { id = c.Id, x = p.x, y = p.y, z = 0 }));
        }

        // ---------- interaction ----------
        public void Select(CardView c)
        {
            var old = selected; selected = c;
            if (old && old.Data != null) { old.Selected = false; old.Apply(old.Data); }
            if (c && c.Data != null) { c.Selected = true; c.Apply(c.Data); }
        }
        void Update()
        {
            // push positions of cards held by an XR controller (or the mouse in the Editor)
            if (Time.time > nextPush) { nextPush = Time.time + .12f; foreach (var c in cards.Values) if (c.Grab.isSelected || mouseDrag == c) PushPosition(c); }
            var m = Mouse.current; if (m == null || cam == null) return;
            var ray = cam.ScreenPointToRay(m.position.ReadValue());
            if (m.leftButton.wasPressedThisFrame && !EventSystem.current.IsPointerOverGameObject() && Physics.Raycast(ray, out var hit) && hit.collider.TryGetComponent(out CardView cv))
            { mouseDrag = cv; Select(cv); mouseOffset = cv.transform.position - ray.GetPoint(hit.distance); }
            if (mouseDrag && m.leftButton.isPressed)
            {
                var pl = new Plane(board.forward, mouseDrag.transform.position);
                if (pl.Raycast(ray, out var d)) { var p = ray.GetPoint(d) + mouseOffset; mouseDrag.transform.position = p; mouseDrag.Target = p; }
            }
            if (mouseDrag && m.leftButton.wasReleasedThisFrame) { PushPosition(mouseDrag); mouseDrag = null; }
        }

        // ---------- actions ----------
        public void AddIdea(string text, string author = null, int slot = -1)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            int n = slot >= 0 ? slot : cards.Count;
            StartCoroutine(Post("card", new CardPost { text = text.Trim(), author = author ?? myName, x = (n % 5 - 2) * 1.8f, y = 1.6f - (n / 5) * 1.2f }));
        }
        void Seed()
        {
            string[] t = { "Compost bins in every dorm", "Meal-sharing app for leftover dining-hall food", "Weekly ugly-produce discount market", "Smaller plate sizes at the cafeteria", "Compost-to-garden program with the biology club", "Real-time surplus alerts for students", "Track waste per tray with a leaderboard", "Partner with local shelters for donations" };
            string[] who = { "Asha", "Ben", "Chen", "Dara" }; int b = cards.Count; for (int i = 0; i < t.Length; i++) AddIdea(t[i], who[i % 4], b + i);
        }
        IEnumerator Export()
        {
            using var r = UnityWebRequest.Get(Url("export")); yield return r.SendWebRequest();
            if (r.result != UnityWebRequest.Result.Success) { Say("Export failed", true); yield break; }
            var path = Path.Combine(Application.persistentDataPath, $"mindspace-{roomCode}.md"); File.WriteAllText(path, r.downloadHandler.text);
            Say($"Saved on device: {path}\nOr open {Url("export")} on a laptop.", true);
        }

        // ---------- UI (world-space, works with XR ray interactors) ----------
        void Say(string s, bool hold = false) { if (hold) holdUntil = Time.time + 8f; else if (Time.time < holdUntil) return; if (status) status.text = s; }
        void BuildUi()
        {
            var es = FindFirstObjectByType<EventSystem>();
            if (!es) es = new GameObject("EventSystem").AddComponent<EventSystem>();
            if (!es.GetComponent<XRUIInputModule>()) { foreach (var im in es.GetComponents<BaseInputModule>()) Destroy(im); es.gameObject.AddComponent<XRUIInputModule>(); }
            var cgo = new GameObject("MindSpacePanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = cam;
            cgo.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = cgo.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(700, 760);
            cgo.transform.position = new Vector3(-1.5f, 1.3f, 1.4f); cgo.transform.rotation = Quaternion.Euler(0, -30, 0); cgo.transform.localScale = Vector3.one * .0011f;
            var bg = new GameObject("Bg", typeof(Image)); bg.transform.SetParent(cgo.transform, false); bg.GetComponent<Image>().color = new Color(.05f, .07f, .12f, .92f); bg.GetComponent<RectTransform>().sizeDelta = rt.sizeDelta;
            var res = new TMP_DefaultControls.Resources();

            Label(cgo.transform, "MIND<color=#6ee7c8>SPACE</color> AI", 0, 340, 56);
            roomInput = MakeInput(res, cgo.transform, "Room code", 0, 270, 400); roomInput.text = roomCode;
            Btn(res, cgo.transform, "Join", 260, 270, 140, () => { roomCode = roomInput.text.Trim().ToUpper(); foreach (var c in cards.Values) Destroy(c.gameObject); cards.Clear(); version = -1; });
            ideaInput = MakeInput(res, cgo.transform, "Type an idea…", -70, 190, 480);
            Btn(res, cgo.transform, "Add", 260, 190, 140, () => { AddIdea(ideaInput.text); ideaInput.text = ""; });
            Btn(res, cgo.transform, "✦ Organize with AI", -180, 110, 320, () => StartCoroutine(Post("organize")));
            Btn(res, cgo.transform, "Load demo ideas", 160, 110, 320, Seed);
            Btn(res, cgo.transform, "▲ Vote", -180, 40, 320, () => { if (selected) StartCoroutine(Post("vote", new VotePost { id = selected.Id, voter = me })); });
            Btn(res, cgo.transform, "★ Decide", 160, 40, 320, () => { if (selected) StartCoroutine(Post("decide", new VotePost { id = selected.Id })); });
            Btn(res, cgo.transform, "Export plan", -180, -30, 320, () => StartCoroutine(Export()));
            Btn(res, cgo.transform, "Clear room", 160, -30, 320, () => StartCoroutine(Post("clear")));
            info = Label(cgo.transform, "", 0, -170, 24, 640, 200);
            status = Label(cgo.transform, "Connecting…", 0, -320, 22, 640, 120);
        }
        TMP_Text Label(Transform p, string t, float x, float y, float size, float w = 640, float h = 80)
        {
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(p, false);
            var tx = go.AddComponent<TextMeshProUGUI>(); tx.text = t; tx.fontSize = size; tx.alignment = TextAlignmentOptions.Center; tx.color = Color.white;
            var r = tx.rectTransform; r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); return tx;
        }
        void Btn(TMP_DefaultControls.Resources res, Transform p, string t, float x, float y, float w, UnityEngine.Events.UnityAction a)
        {
            var go = TMP_DefaultControls.CreateButton(res); go.transform.SetParent(p, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, 56);
            go.GetComponentInChildren<TMP_Text>().text = t; go.GetComponentInChildren<TMP_Text>().fontSize = 24;
            go.GetComponent<Button>().onClick.AddListener(a);
        }
        TMP_InputField MakeInput(TMP_DefaultControls.Resources res, Transform p, string ph, float x, float y, float w)
        {
            var go = TMP_DefaultControls.CreateInputField(res); go.transform.SetParent(p, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, 56);
            var f = go.GetComponent<TMP_InputField>(); ((TMP_Text)f.placeholder).text = ph; f.pointSize = 24; return f;
        }
    }
}

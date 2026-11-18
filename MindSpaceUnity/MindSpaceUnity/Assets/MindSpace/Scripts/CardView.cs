using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MindSpace
{
    /// A floating idea card. Grabbable with XR controllers (XRGrabInteractable).
    public class CardView : MonoBehaviour
    {
        public string Id; public CardDto Data; public bool Selected, Decided;
        public XRGrabInteractable Grab; public Vector3 Target;
        Renderer rend; TextMeshPro label; MindSpaceApp app;
        static readonly Color[] Palette = { new(.15f,.75f,.65f), new(.9f,.6f,.25f), new(.55f,.4f,.9f), new(.9f,.35f,.5f), new(.3f,.6f,.95f), new(.55f,.8f,.3f), new(.85f,.4f,.3f) };

        public static CardView Create(MindSpaceApp app, Transform parent, CardDto d)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Card_" + d.id; go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(.64f, .4f, .02f);
            var v = go.AddComponent<CardView>(); v.Id = d.id; v.app = app; v.rend = go.GetComponent<Renderer>();
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            v.Grab = go.AddComponent<XRGrabInteractable>();
            v.Grab.trackRotation = false; v.Grab.throwOnDetach = false;
            v.Grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            v.Grab.selectEntered.AddListener(_ => app.Select(v));
            var t = new GameObject("Label"); t.transform.SetParent(go.transform, false);
            t.transform.localPosition = new Vector3(0, 0, -.6f);          // in front of the slab
            t.transform.localScale = new Vector3(1f / .64f, 1f / .4f, 1f / .02f) * .1f;
            v.label = t.AddComponent<TextMeshPro>(); v.label.alignment = TextAlignmentOptions.Center;
            v.label.fontSize = 1.6f; v.label.rectTransform.sizeDelta = new Vector2(6f, 3.6f); v.label.color = Color.white;
            return v;
        }

        public void Apply(CardDto d)
        {
            Data = d;
            var c = d.group >= 0 ? Palette[d.group % Palette.Length] : new Color(.18f, .22f, .32f);
            var k = Selected ? 1.4f : .8f; rend.material.color = Decided ? new Color(1f, .82f, .3f) : new Color(c.r * k, c.g * k, c.b * k, 1f);
            var head = string.IsNullOrEmpty(d.groupLabel) ? "" : $"<size=70%><b>{Safe(d.groupLabel).ToUpper()}</b></size>\n";
            label.text = $"{head}{Safe(d.text)}\n<size=60%>{Safe(d.author)}  ▲ {d.votes.Count}{(Decided ? "  ★" : "")}</size>";
        }

        static string Safe(string t) => (t ?? "").Replace("<", "‹");

        void Update()
        {
            if (!Grab.isSelected && !app.IsMouseDragging(this))
                transform.position = Vector3.Lerp(transform.position, Target, .25f);
        }
    }
}

using UnityEngine;
using Ratones.Core;

namespace Ratones.Gameplay
{
    public sealed class MouseView : MonoBehaviour
    {
        Transform body, leftPaw, rightPaw, shadow, sticky;
        LineRenderer tail;
        TrailRenderer trail;
        float phase;
        Material ringMaterial;
        public void Build(KitchenView view, Color color, Transform customPrefab)
        {
            body = new GameObject("Visual del ratón").transform; body.SetParent(transform, false);
            var fur = view.Material("fur", new Color(.62f, .64f, .66f));
            var pink = view.Material("pink", new Color(1f, .56f, .57f));
            var dark = view.Material("eyes", new Color(.035f, .045f, .065f));
            var scarf = view.Material("scarf" + ColorUtility.ToHtmlStringRGB(color), color);
            if (customPrefab != null) Instantiate(customPrefab, body, false);
            else
            {
                KitchenView.Shape("Cuerpo", PrimitiveType.Sphere, body, new Vector3(0, 1.05f, -.1f), new Vector3(1.7f, 1.75f, 2.3f), fur);
                KitchenView.Shape("Cabeza", PrimitiveType.Sphere, body, new Vector3(0, 1.65f, 1), new Vector3(1.4f, 1.35f, 1.45f), fur);
                KitchenView.Shape("Hocico", PrimitiveType.Sphere, body, new Vector3(0, 1.35f, 1.7f), new Vector3(.9f, .65f, 1), fur);
                KitchenView.Shape("Nariz", PrimitiveType.Sphere, body, new Vector3(0, 1.43f, 2.12f), Vector3.one * .34f, pink);
                for (int s = -1; s <= 1; s += 2)
                {
                    KitchenView.Shape("Oreja", PrimitiveType.Sphere, body, new Vector3(s * .73f, 2.35f, .75f), new Vector3(.9f, 1.04f, .34f), fur);
                    KitchenView.Shape("Interior de oreja", PrimitiveType.Sphere, body, new Vector3(s * .73f, 2.35f, .91f), new Vector3(.66f, .8f, .09f), pink);
                    KitchenView.Shape("Ojo", PrimitiveType.Sphere, body, new Vector3(s * .52f, 1.86f, 1.47f), new Vector3(.27f, .32f, .22f), dark);
                    KitchenView.Shape("Brillo del ojo", PrimitiveType.Sphere, body, new Vector3(s * .52f - .045f, 1.94f, 1.56f), Vector3.one * .085f, view.Material("eyeLight", Color.white));
                    for (int w = 0; w < 2; w++)
                    {
                        var whisker = KitchenView.Shape("Bigote", PrimitiveType.Cube, body,
                            new Vector3(s * .67f, 1.4f + w * .14f, 1.8f), new Vector3(.83f, .03f, .03f), dark);
                        whisker.transform.localRotation = Quaternion.Euler(0, s * (w == 0 ? 18 : -18), s * 6);
                    }
                }
                leftPaw = KitchenView.Shape("Pata izquierda", PrimitiveType.Sphere, body, new Vector3(-.67f, .19f, .3f), new Vector3(.57f, .4f, .9f), pink).transform;
                rightPaw = KitchenView.Shape("Pata derecha", PrimitiveType.Sphere, body, new Vector3(.67f, .19f, .3f), new Vector3(.57f, .4f, .9f), pink).transform;
                KitchenView.Shape("Pañuelo", PrimitiveType.Cylinder, body, new Vector3(0, 1.22f, .75f), new Vector3(1.47f, .13f, 1.22f), scarf);
                var knot = KitchenView.Shape("Pico del pañuelo", PrimitiveType.Cube, body, new Vector3(.67f, 1.3f, .3f), new Vector3(.65f, .2f, .9f), scarf);
                knot.transform.localRotation = Quaternion.Euler(0, 28, 25);
                tail = new GameObject("Cola").AddComponent<LineRenderer>(); tail.transform.SetParent(body, false);
                tail.useWorldSpace = false; tail.positionCount = 8; tail.startWidth = .17f; tail.endWidth = .055f;
                tail.sharedMaterial = pink; tail.numCapVertices = 3;
            }
            shadow = KitchenView.Shape("Color del jugador", PrimitiveType.Cylinder, transform,
                new Vector3(0, .08f, 0), new Vector3(2.7f, .025f, 2.7f), scarf).transform;
            sticky = KitchenView.Shape("Pegamento", PrimitiveType.Cylinder, transform,
                new Vector3(0, .17f, 0), new Vector3(3.3f, .08f, 3.3f), view.Material("sticky", new Color(.68f, .3f, 1f), true)).transform;
            sticky.gameObject.SetActive(false);
            trail = new GameObject("Estela de azúcar").AddComponent<TrailRenderer>(); trail.transform.SetParent(transform, false);
            trail.transform.localPosition = new Vector3(0, .6f, -.8f);
            ringMaterial = new Material(Resources.Load<Shader>("Ratones/Trail")); ringMaterial.color = new Color(.1f, .8f, 1f);
            trail.sharedMaterial = ringMaterial; trail.time = .27f; trail.startWidth = .8f; trail.endWidth = 0;
            trail.minVertexDistance = .15f; trail.emitting = false;
        }
        public void Apply(PlayerState state, bool winner, float dt)
        {
            Vector3 target = new Vector3(state.Position.X, 0, state.Position.Z);
            float distance = Vector3.Distance(transform.position, target);
            bool moving = distance > .065f;
            transform.position = Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-24 * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, state.Heading, 0), dt * 18);
            phase += dt * (moving ? (state.BoostLeft > 0 ? 22 : 16) : 3);
            float bounce = moving ? Mathf.Abs(Mathf.Sin(phase)) * .15f : Mathf.Sin(phase) * .04f;
            if (winner) bounce = Mathf.Abs(Mathf.Sin(Time.time * 7)) * 1.1f;
            body.localPosition = new Vector3(state.StickyLeft > 0 ? Mathf.Sin(Time.time * 35) * .055f : 0, bounce, 0);
            if (leftPaw != null)
            {
                leftPaw.localPosition = new Vector3(-.67f, .19f, .3f + (moving ? Mathf.Sin(phase) * .42f : 0));
                rightPaw.localPosition = new Vector3(.67f, .19f, .3f - (moving ? Mathf.Sin(phase) * .42f : 0));
            }
            if (tail != null) for (int i = 0; i < 8; i++)
                tail.SetPosition(i, new Vector3(Mathf.Sin(phase * .45f - i * .4f) * i * .06f, .55f - i * .04f, -1 - i * .29f));
            sticky.gameObject.SetActive(state.StickyLeft > 0);
            trail.emitting = state.BoostLeft > 0 && state.StickyLeft <= 0 && moving;
        }
        void Update()
        {
            // Los ratones del menú también respiran.
            if (transform.localScale.x > 1.5f && body != null)
                body.localPosition = new Vector3(0, Mathf.Sin(Time.time * 2.5f + transform.position.x) * .05f, 0);
        }
        void OnDestroy() { if (ringMaterial != null) Destroy(ringMaterial); }
    }
}

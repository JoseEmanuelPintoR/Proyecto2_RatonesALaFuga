using System.Collections.Generic;
using UnityEngine;

namespace Ratones.Basic
{
    public sealed class BoardView : MonoBehaviour
    {
        public PlayerView PlayerPrefab;
        public GameObject Queso, Fresa, Pie, Chocolate, Trampa;
        public FollowCamera CameraFollow;
        readonly Dictionary<int, PlayerView> players = new Dictionary<int, PlayerView>();
        readonly Dictionary<int, GameObject> items = new Dictionary<int, GameObject>();
        int round = -1;
        void Start()
        { if (!Session.Ensure().Connected) Session.Ensure().Go("Conexion"); }
        void Update()
        {
            Session session = Session.Ensure(); State state = session.State;
            if (state == null) return;
            if (state.Round != round)
            {
                foreach (PlayerView p in players.Values) Destroy(p.gameObject);
                foreach (GameObject i in items.Values) Destroy(i);
                players.Clear(); items.Clear(); round = state.Round;
                foreach (Item item in state.Items)
                {
                    GameObject prefab = item.Kind == ItemKind.Queso ? Queso : item.Kind == ItemKind.Fresa ? Fresa : item.Kind == ItemKind.Pie ? Pie : item.Kind == ItemKind.Chocolate ? Chocolate : Trampa;
                    items[item.Id] = Instantiate(prefab, new Vector3(item.Position.X, .6f, item.Position.Z), Quaternion.identity, transform);
                }
            }
            foreach (Player p in state.Players)
            {
                PlayerView view;
                if (!players.TryGetValue(p.Id, out view)) { view = Instantiate(PlayerPrefab, transform); view.name = p.Name; players[p.Id] = view; }
                view.Show(p);
                if (p.Id == session.LocalId) CameraFollow.Target = view.transform;
            }
            foreach (Item item in state.Items)
            {
                GameObject model;
                if (!items.TryGetValue(item.Id, out model)) continue;
                model.SetActive(item.Active);
                model.transform.rotation = Quaternion.Euler(0, Time.time * 35 + item.Id * 17, 0);
            }
        }
    }
}

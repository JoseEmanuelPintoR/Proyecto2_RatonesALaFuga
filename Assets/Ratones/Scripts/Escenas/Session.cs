using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ratones.Basic
{
    // Único objeto persistente: mantiene la conexión al cambiar de escena.
    public sealed class Session : MonoBehaviour
    {
        public static Session Instance { get; private set; }
        public AudioClip MusicClip;
        public AudioClip CollectClip;
        public AudioClip PowerClip;
        public State State { get; private set; }
        public int LocalId { get; private set; } = -1;
        public string RoomCode { get; private set; } = "";
        public string Status { get; private set; } = "";
        public bool IsHost { get { return server != null; } }
        public bool IsConnecting { get { return client != null && !client.Ready && client.Error == null; } }
        public bool Connected { get { return server != null || (client != null && client.Ready); } }
        public string PlayerName { get; private set; }
        public int KeyColor { get; private set; }
        public int AuraColor { get; private set; }
        public AudioSource Music { get; private set; }
        public AudioSource Effects { get; private set; }
        GameServer server;
        GameClient client;
        LanBeacon beacon;
        Phase? previousPhase;
        int previousRound = -1, previousScore;
        float previousBoost, previousFreeze;
        bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Ensure(); }
        public static Session Ensure()
        {
            if (Instance == null)
            {
                var prefab = Resources.Load<GameObject>("Session");
                if (prefab != null) Instantiate(prefab);
                else new GameObject("Session").AddComponent<Session>();
            }
            return Instance;
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            Application.runInBackground = true; Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.LandscapeLeft; Screen.sleepTimeout = SleepTimeout.NeverSleep;
            PlayerName = PlayerPrefs.GetString("RF2.Name", "Jugador");
            KeyColor = Simulation.ColorIndex(PlayerPrefs.GetInt("RF2.Key", 5));
            AuraColor = Simulation.ColorIndex(PlayerPrefs.GetInt("RF2.Aura", 3));
            Music = gameObject.AddComponent<AudioSource>(); Music.loop = true; Music.clip = MusicClip; Music.spatialBlend = 0;
            Effects = gameObject.AddComponent<AudioSource>(); Effects.spatialBlend = 0;
            ApplyVolume(); if (MusicClip != null) Music.Play(); loaded = true;
        }
        public void SaveProfile(string name, int key, int aura)
        {
            PlayerName = Simulation.CleanName(name); KeyColor = Simulation.ColorIndex(key); AuraColor = Simulation.ColorIndex(aura);
            PlayerPrefs.SetString("RF2.Name", PlayerName); PlayerPrefs.SetInt("RF2.Key", KeyColor); PlayerPrefs.SetInt("RF2.Aura", AuraColor);
            PlayerPrefs.Save();
        }
        public void ApplyVolume()
        {
            if (Music != null) Music.volume = PlayerPrefs.GetFloat("RF2.Music", .5f);
            if (Effects != null) Effects.volume = PlayerPrefs.GetFloat("RF2.Effects", .7f);
        }
        public void CreateRoom()
        {
            Stop(); RoomCode = LanDiscovery.NewCode();
            try
            {
                server = new GameServer(RoomCode, PlayerName, KeyColor, AuraColor, Environment.TickCount);
                beacon = new LanBeacon(RoomCode, server.Port);
                State = server.Simulation.State; LocalId = server.HostId;
                Status = ""; Go("Lobby");
            }
            catch (Exception e) { Stop(); Status = "No se pudo crear la sala: " + e.Message; }
        }
        public void JoinRoom(string code)
        {
            code = (code ?? "").Trim();
            if (!LanDiscovery.ValidCode(code)) { Status = "Escribe los 6 números del código."; return; }
            Stop(); RoomCode = code;
            client = new GameClient(code, PlayerName, KeyColor, AuraColor); Status = "Buscando sala…";
        }
        void Update()
        {
            if (!loaded) return;
            float dt = Time.unscaledDeltaTime;
            if (server != null)
            {
                if (dt > 3) { Stop(); Status = "El anfitrión quedó suspendido. Crea otra sala."; Go("Conexion"); return; }
                server.Update(dt); State = server.Simulation.State; LocalId = server.HostId;
            }
            if (client != null)
            {
                client.Update(dt); Status = client.Ready ? "" : client.Status;
                if (client.Error != null)
                { string error = client.Error; Stop(); Status = error; Go("Conexion"); return; }
                if (client.Ready) { State = client.State; LocalId = client.Id; }
            }
            if (State == null) return;
            if (previousPhase != State.Phase || previousRound != State.Round)
            {
                previousPhase = State.Phase; previousRound = State.Round;
                if (State.Phase == Phase.Lobby) Go("Lobby");
                else if (State.Phase == Phase.Results) Go("Resultados");
                else Go("Juego");
            }
            Player player = State.Player(LocalId);
            if (player != null)
            {
                if (player.Score > previousScore && CollectClip != null) Effects.PlayOneShot(CollectClip);
                if ((player.BoostLeft > previousBoost + .5f || player.FreezeLeft > previousFreeze + .5f) && PowerClip != null) Effects.PlayOneShot(PowerClip);
                previousScore = player.Score; previousBoost = player.BoostLeft; previousFreeze = player.FreezeLeft;
            }
        }
        public void Move(Vector2 value)
        {
            value = Vector2.ClampMagnitude(value, 1);
            if (server != null) server.Simulation.Move(LocalId, value.x, value.y);
            if (client != null) client.Move(value.x, value.y);
        }
        public void UseSpeed() { Use(Power.Speed); }
        public void UseFreeze() { Use(Power.Freeze); }
        void Use(Power power)
        {
            if (server != null) server.Simulation.Use(LocalId, power);
            if (client != null) client.Use(power);
        }
        public void StartRound() { if (server != null) server.Simulation.Start(LocalId); }
        public void BackToLobby()
        {
            if (server != null) server.Simulation.Lobby(LocalId);
            if (Connected) Go("Lobby"); else Go("Conexion");
        }
        public void LeaveToMenu() { Stop(); Go("MenuInicio"); }
        public void LeaveToConnection() { Stop(); Go("Conexion"); }
        public void Go(string scene)
        { if (SceneManager.GetActiveScene().name != scene) SceneManager.LoadScene(scene); }
        public void Quit()
        {
            Stop();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        void Stop()
        {
            if (client != null) client.Dispose(); if (beacon != null) beacon.Dispose(); if (server != null) server.Dispose();
            client = null; beacon = null; server = null; State = null; LocalId = -1;
            RoomCode = ""; previousPhase = null; previousRound = -1; previousScore = 0; previousBoost = previousFreeze = 0;
            Status = "";
        }
        void OnApplicationFocus(bool focused) { if (!focused) Move(Vector2.zero); }
        void OnApplicationPause(bool paused)
        {
            if (paused && loaded && (server != null || client != null))
            { Stop(); Status = "Saliste de la sala al poner el juego en segundo plano."; Go("Conexion"); }
        }
        void OnDestroy() { if (Instance == this) { Stop(); Instance = null; } }
    }
}

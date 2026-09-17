using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using Ratones.Core;
using Ratones.Network;

namespace Ratones.Gameplay
{
    public sealed class RatonesGame : MonoBehaviour
    {
        [Header("Opcional: sustituir los modelos sin cambiar las reglas")]
        public Transform MouseVisualPrefab;
        public Transform CheeseVisualPrefab;
        public KitchenView World { get; private set; }
        public bool IsHost { get { return server != null; } }
        public bool Connecting { get { return client != null && client.Connecting; } }
        public string RoomAddress { get; private set; } = "127.0.0.1";
        public int RoomPort { get; private set; } = GameRules.Port;
        public bool SoundEnabled { get { return soundOn; } }
        public bool ShadowsEnabled { get { return shadowsOn; } }
        GameServer server;
        GameClient client;
        GameHud hud;
        GameAudio sounds;
        MatchState state;
        int ownId = -1, lastScore, lastSecond = -1, lastRound = -1;
        MatchPhase previousPhase;
        float lastBoost, lastSticky, sendClock;
        bool boostRequested, stickyRequested, soundOn, shadowsOn, inputBlocked;
        string message = "";

        void Awake()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; Screen.orientation = ScreenOrientation.LandscapeLeft;
            QualitySettings.vSyncCount = 0;
            var worldObject = new GameObject("Cocina gigante"); worldObject.transform.SetParent(transform);
            World = worldObject.AddComponent<KitchenView>(); World.MouseVisualPrefab = MouseVisualPrefab;
            World.CheeseVisualPrefab = CheeseVisualPrefab; World.Build();
            hud = new GameObject("Interfaz").AddComponent<GameHud>(); hud.transform.SetParent(transform); hud.Build(this);
            sounds = gameObject.AddComponent<GameAudio>(); sounds.Build();
            soundOn = PlayerPrefs.GetInt("Ratones.Sound", 1) == 1;
            shadowsOn = PlayerPrefs.GetInt("Ratones.Shadows", 1) == 1;
            sounds.SetEnabled(soundOn); World.SetShadows(shadowsOn);
        }
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // Un anfitrión suspendido no debe continuar una ronda con un reloj distinto.
            if (dt > 3 && server != null && state != null && state.Phase != MatchPhase.Lobby)
            { LeaveRoom(); message = "Se cerró la sala porque el anfitrión quedó suspendido."; }
            if (server != null)
            { server.Update(dt); state = server.Simulation.State; ownId = server.HostPlayerId; }
            if (client != null)
            {
                client.Update(dt); state = client.State; ownId = client.PlayerId;
                if (client.Error != null)
                { string error = client.Error; LeaveRoom(); message = error; }
            }
            if (state != null && state.Phase == MatchPhase.Playing)
            {
                Vector2 move = Vector2.zero;
                if (!inputBlocked && Application.isFocused)
                {
                    if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1;
                    if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1;
                    if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1;
                    if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1;
                    if (hud.Joystick != null && hud.Joystick.Value.sqrMagnitude > .01f) move = hud.Joystick.Value;
                    boostRequested |= Input.GetKeyDown(KeyCode.E);
                    stickyRequested |= Input.GetKeyDown(KeyCode.Q);
                    if (Input.GetKeyDown(KeyCode.Escape)) ToggleLeaveDialog();
                }
                move = Vector2.ClampMagnitude(move, 1);
                sendClock += dt;
                if (sendClock >= 1f / GameRules.TickRate || boostRequested || stickyRequested)
                {
                    sendClock = 0;
                    if (server != null) server.Simulation.Input(ownId, move.x, move.y, boostRequested, stickyRequested);
                    if (client != null) client.Input(move.x, move.y, boostRequested, stickyRequested);
                    boostRequested = stickyRequested = false;
                }
            }
            if (state != null) Feedback();
            World.Show(state, ownId, dt);
            hud.Draw(state, ownId, Connecting ? "Buscando la sala…" : message);
        }
        void Feedback()
        {
            PlayerState p = state.Player(ownId);
            if (p == null) return;
            if (lastRound != state.Round) { lastRound = state.Round; lastScore = 0; lastBoost = lastSticky = 0; lastSecond = -1; }
            if (p.Score > lastScore) sounds.Cheese();
            if (p.BoostLeft > lastBoost + .5f) sounds.Boost();
            if (p.StickyLeft > lastSticky + .5f) sounds.Sticky();
            int second = Mathf.CeilToInt(state.TimeLeft);
            if (state.Phase == MatchPhase.Playing && second <= 10 && second > 0 && second != lastSecond) sounds.Tick();
            if (state.Phase == MatchPhase.Results && previousPhase != MatchPhase.Results) sounds.Finish(state.Winners.Contains(ownId));
            lastScore = p.Score; lastBoost = p.BoostLeft; lastSticky = p.StickyLeft; lastSecond = second; previousPhase = state.Phase;
        }

        public void CreateRoom(string name, string port)
        {
            int parsed;
            if (!ValidPort(port, out parsed)) return;
            LeaveRoom();
            try
            {
                server = new GameServer(name, parsed, Environment.TickCount);
                ownId = server.HostPlayerId; state = server.Simulation.State;
                RoomPort = parsed; RoomAddress = LocalAddress();
                PlayerPrefs.SetString("Ratones.Name", name); PlayerPrefs.Save();
            }
            catch (Exception e)
            { if (server != null) server.Dispose(); server = null; message = "No se pudo crear la sala. Prueba otro puerto. " + e.Message; }
        }
        public void JoinRoom(string name, string address, string port)
        {
            int parsed;
            if (!ValidPort(port, out parsed)) return;
            IPAddress ip;
            if (!IPAddress.TryParse(address.Trim(), out ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            { message = "Escribe la dirección IPv4 del anfitrión, por ejemplo 192.168.1.20."; return; }
            LeaveRoom(); RoomAddress = address.Trim(); RoomPort = parsed;
            PlayerPrefs.SetString("Ratones.Name", name); PlayerPrefs.SetString("Ratones.IP", RoomAddress); PlayerPrefs.Save();
            client = new GameClient(RoomAddress, parsed, name);
            message = "Buscando la sala…";
        }
        bool ValidPort(string port, out int parsed)
        {
            if (!int.TryParse(port, out parsed) || parsed < 1 || parsed > 65535)
            { message = "El puerto debe estar entre 1 y 65535."; return false; }
            return true;
        }
        static string LocalAddress()
        {
            try
            {
                var addresses = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                    .Select(a => a.Address.ToString()).Distinct().Take(3).ToArray();
                if (addresses.Length > 0) return string.Join(" / ", addresses);
            }
            catch { }
            return "Consulta la IP Wi-Fi en Ajustes";
        }
        public void StartRound() { if (server != null) server.Simulation.Start(ownId); }
        public void ReturnLobby() { if (server != null) { server.Simulation.ReturnToLobby(ownId); inputBlocked = false; } }
        public void RequestBoost() { if (!inputBlocked) boostRequested = true; }
        public void RequestSticky() { if (!inputBlocked) stickyRequested = true; }
        public void ToggleLeaveDialog()
        {
            if (inputBlocked) return;
            inputBlocked = true; boostRequested = stickyRequested = false;
            if (hud.Joystick != null) hud.Joystick.ResetInput();
            hud.LeaveDialog();
        }
        public void ResumeInput() { inputBlocked = false; }
        public void LeaveRoom()
        {
            if (client != null) { client.Dispose(); client = null; }
            if (server != null) { server.Dispose(); server = null; }
            state = null; ownId = -1; message = "Conecta los dispositivos a la misma red Wi-Fi.";
            boostRequested = stickyRequested = inputBlocked = false;
            lastRound = -1; previousPhase = MatchPhase.Lobby;
            if (hud != null) hud.RefreshPage();
        }
        public void ToggleSound()
        {
            soundOn = !soundOn; sounds.SetEnabled(soundOn);
            PlayerPrefs.SetInt("Ratones.Sound", soundOn ? 1 : 0); PlayerPrefs.Save();
            message = "Sonido " + (soundOn ? "activado." : "desactivado.");
        }
        public void ToggleShadows()
        {
            shadowsOn = !shadowsOn; World.SetShadows(shadowsOn);
            PlayerPrefs.SetInt("Ratones.Shadows", shadowsOn ? 1 : 0); PlayerPrefs.Save();
            message = "Sombras " + (shadowsOn ? "activadas." : "desactivadas.");
        }
        public void Quit()
        {
            LeaveRoom();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            if (hud != null && hud.Joystick != null) hud.Joystick.ResetInput();
            if (server != null) server.Simulation.Input(ownId, 0, 0, false, false);
            if (client != null) client.Input(0, 0, false, false);
        }
        void OnApplicationPause(bool paused)
        {
            if (!paused || (client == null && server == null)) return;
            LeaveRoom(); message = "Saliste de la sala al poner el juego en segundo plano.";
        }
        void OnDestroy() { if (client != null) client.Dispose(); if (server != null) server.Dispose(); }
    }
}

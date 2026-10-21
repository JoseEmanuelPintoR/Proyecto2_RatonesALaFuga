using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ratones.Basic.Editor
{
    // Aplica el arte de Assets/Ratones/UI, la fuente Gluten y el acomodo de los diseños a las escenas.
    // Se puede repetir: cada vez deja las posiciones como en el diseño (referencia 1280x720).
    public static class UITools
    {
        const string Art = "Assets/Ratones/UI/";
        const string Fonts = "Assets/Ratones/fonts/Gluten/";
        const string Scenes = "Assets/Ratones/Scenes/";
        const string PlayerPrefab = "Assets/Ratones/Prefabs/JugadorBasico.prefab";
        // Velocidad de los cuadros de Confeti: el GIF original usa 40 ms por cuadro.
        const float ConfettiFps = 25;
        const int ConfettiRepeats = 3;
        static readonly string[] SceneNames = { "MenuInicio", "Conexion", "Lobby", "Personalizar", "Configuracion", "Juego", "Resultados" };
        // Mismo orden que Palette.Colors.
        static readonly string[] AuraNames = { "ROJA", "AZUL", "VERDE", "ROSADA", "NARANJA", "AMARILLA", "NEGRA", "BLANCA", "MORADA", "CAFE", "TURQUESA", "MENTA" };

        static readonly Vector2 TopLeft = new Vector2(0, 1), TopRight = new Vector2(1, 1), TopCenter = new Vector2(.5f, 1),
            Center = new Vector2(.5f, .5f), BottomLeft = Vector2.zero, BottomCenter = new Vector2(.5f, 0), LeftMiddle = new Vector2(0, .5f);
        static readonly Color Dark = new Color(.24f, .12f, .05f), Cream = new Color(1, .95f, .85f);

        static Font bold, regular, thin;
        static Scene scene;
        static int warnings;

        [MenuItem("Ratones/UI nueva/Aplicar a la escena abierta")]
        public static void ApplyOpen()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de aplicar la UI nueva."); return; }
            if (!Prepare()) return;
            Scene open = SceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Aplicar UI nueva"); int group = Undo.GetCurrentGroup();
            Apply(open);
            EditorSceneManager.MarkSceneDirty(open); Undo.CollapseUndoOperations(group);
            Debug.Log("UI nueva aplicada a " + open.name + " (" + warnings + " avisos). Guarda con Ctrl+S.");
        }

        [MenuItem("Ratones/UI nueva/Aplicar a todas las escenas")]
        public static void ApplyAll()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de aplicar la UI nueva."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Prepare()) return;
            string current = SceneManager.GetActiveScene().path;
            int total = 0;
            foreach (string name in SceneNames)
            {
                string path = Scenes + name + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { Debug.LogWarning("No se encontró " + path); continue; }
                Scene opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Apply(opened); total += warnings;
                EditorSceneManager.SaveScene(opened);
            }
            // Las auras del ratón y de los items también usan las imágenes de AURAS GRANDES.
            ModelTools.ApplyAuras();
            if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
            Debug.Log("UI nueva aplicada y guardada en todas las escenas (" + total + " avisos).");
        }

        static bool Prepare()
        {
            // Los PNG que se subieron antes del importador quedaron como Default: se pasan a Sprite.
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art.TrimEnd('/') }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (importer != null && UISpriteImporter.Configure(importer)) importer.SaveAndReimport();
            }
            bold = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Gluten-Bold.ttf");
            regular = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Gluten-Regular.ttf");
            thin = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Gluten-Thin.ttf");
            if (bold == null || regular == null || thin == null) { Debug.LogError("Falta Gluten-Bold, Gluten-Regular o Gluten-Thin en " + Fonts); return false; }
            // El juego es solo horizontal.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            AppNameAndIcon();
            PlayerNameFont();
            return true;
        }

        // Nombre de la app y su ícono (PORTADA APK) para todas las plataformas.
        static void AppNameAndIcon()
        {
            PlayerSettings.productName = "Ratones a la fuga";
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "PORTADA APK.png");
            if (icon == null) { Debug.LogWarning("No se encontró " + Art + "PORTADA APK.png"); return; }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        // El nombre sobre cada ratón (PlayerView) usa Gluten Bold.
        static void PlayerNameFont()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab) == null) { Debug.LogWarning("No se encontró " + PlayerPrefab); return; }
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                PlayerView view = root.GetComponent<PlayerView>();
                if (view != null && view.NameFont != bold) { view.NameFont = bold; PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Apply(Scene target)
        {
            scene = target; warnings = 0;
            ApplyFonts();
            switch (scene.name)
            {
                case "MenuInicio": Menu(); break;
                case "Conexion": Connection(); break;
                case "Lobby": Lobby(); break;
                case "Personalizar": Personalize(); break;
                case "Configuracion": Settings(); break;
                case "Juego": Game(); break;
                case "Resultados": Results(); break;
                default: Debug.Log(scene.name + ": solo se cambiaron las fuentes."); break;
            }
            Background();
        }

        // Fondo de cada escena (UI/FONDOS) en el RawImage FondoJPG. Juego usa los azulejos laterales.
        static readonly Dictionary<string, string> Backgrounds = new Dictionary<string, string>
        {
            { "MenuInicio", "FONDO_INICIO" }, { "Conexion", "FONDO_JUGAR" }, { "Lobby", "FONDO_CREARPARTIDA" },
            { "Personalizar", "FONDO_PERSONALIZAR" }, { "Configuracion", "FONDO_CONFIGURACION" }, { "Resultados", "FONDO_PODIO" },
        };
        static void Background()
        {
            string file;
            if (!Backgrounds.TryGetValue(scene.name, out file)) return;
            Transform t = Named("FondoJPG");
            RawImage raw = t != null ? t.GetComponent<RawImage>() : null;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "FONDOS/" + file + ".png");
            if (raw == null || texture == null) { Warn("No se pudo poner " + file + " en FondoJPG."); return; }
            Undo.RecordObject(raw, "Fondo");
            raw.texture = texture; raw.color = Color.white; raw.uvRect = new Rect(0, 0, 1, 1); raw.raycastTarget = false;
            // Cubre toda la pantalla sin deformarse (en celulares más anchos que 16:9 recorta arriba y abajo).
            AspectRatioFitter fitter = raw.GetComponent<AspectRatioFitter>() ?? Undo.AddComponent<AspectRatioFitter>(raw.gameObject);
            Undo.RecordObject(fitter, "Fondo");
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fitter.aspectRatio = (float)texture.width / texture.height;
        }

        // ---------- Fuentes: títulos Bold, subtítulos Regular, descripciones Thin ----------

        static void ApplyFonts()
        {
            var bolds = new HashSet<Text>(); var regulars = new HashSet<Text>(); var thins = new HashSet<Text>();
            var results = Find<ResultsUI>();
            if (results != null) { bolds.Add(results.Title); Add(regulars, results.Names); Add(regulars, results.Places); Add(thins, results.Scores); bolds.Add(results.Fourth); }
            var game = Find<GameUI>();
            if (game != null) { bolds.Add(game.Timer); bolds.Add(game.Countdown); Add(regulars, game.PlayerLabels); thins.Add(game.OwnScore); bolds.Add(game.Inventory); }
            var lobby = Find<LobbyUI>();
            if (lobby != null) { regulars.Add(lobby.CodeText); Add(thins, lobby.Names); thins.Add(lobby.Message); }
            var nav = Find<NavigationUI>();
            if (nav != null) { thins.Add(nav.Status); thins.Add(nav.Name); }
            var personalize = Find<PersonalizeUI>();
            if (personalize != null) thins.Add(personalize.Selected);
            var settings = Find<SettingsUI>();
            if (settings != null) { thins.Add(settings.MusicValue); thins.Add(settings.EffectsValue); }

            foreach (Text text in All<Text>())
            {
                Font font = bolds.Contains(text) || text.name == "Titulo" ? bold : thins.Contains(text) ? thin : regulars.Contains(text) ? regular : Pick(text);
                if (text.font == font) continue;
                Undo.RecordObject(text, "Fuente Gluten"); text.font = font;
            }
        }
        static Font Pick(Text text)
        {
            InputField input = text.GetComponentInParent<InputField>(true);
            if (input != null) return input.placeholder == text ? thin : regular;
            if (text.fontSize >= 32) return bold;
            if (text.GetComponentInParent<Selectable>(true) != null) return regular;
            string value = (text.text ?? "").Trim();
            bool caps = value.Any(char.IsLetter) && value == value.ToUpperInvariant();
            return text.fontSize >= 25 || caps ? regular : thin;
        }
        static void Add(HashSet<Text> set, IEnumerable<Text> texts) { if (texts != null) foreach (Text t in texts) if (t != null) set.Add(t); }

        // ---------- Escenas ----------

        static void Menu()
        {
            SkinButton("Jugar", "MENU/BOTON_JUGAR");
            SkinButton("Personalizar", "MENU/BOTON_PERSONALIZAR");
            SkinButton("Configuración", "MENU/BOTON_CONFIGURACION");
            SkinButton("Salir", "MENU/BOTON_SALIR");
            // El logo reemplaza al título de texto.
            Transform title = Named("Titulo");
            if (title == null) { Warn("No se encontró Titulo."); return; }
            Layout(Decoration("Logo", "MENU/LOGO", title.parent, title.GetSiblingIndex()), Center, new Vector2(0, 185), new Vector2(440, 210));
            Hide(title.gameObject);
        }

        static void Connection()
        {
            // PrepareJoin solo mueve estos botones en Y; el tamaño se pone aquí.
            Layout(SkinButton("Crear partida", "PARTIDA/BOTON_CREARPARTIDA"), Center, Vector2.zero, new Vector2(250, 120));
            Layout(SkinButton("Unirse", "PARTIDA/BOTON_UNIRSE"), Center, Vector2.zero, new Vector2(250, 120));
            Corner(SkinButton("Volver", "PARTIDA/BOTON_REGRESAR"), false);
            // El banner JUGAR reemplaza al título de texto.
            Text title = FindText("Jugar");
            if (title != null)
            {
                Layout(Decoration("TituloJugar", "PARTIDA/TituloJugar", title.transform.parent, title.transform.GetSiblingIndex()), Center, new Vector2(0, 255), new Vector2(430, 122));
                Hide(title.gameObject);
            }
            else if (Named("TituloJugar") == null) Warn("No se encontró el título Jugar.");
            // Quita Entrar y acomoda el desplegable: Unirse abre el código y, con código, entra.
            MenuTools.PrepareJoin();
        }

        static void Lobby()
        {
            LobbyUI lobby = Find<LobbyUI>(); if (lobby == null) { Warn("Falta LobbyUI."); return; }
            SkinButton("Iniciar", "INICIAR PARTIDA/BOTON_INICIAR");
            Corner(SkinButton("Personalizar", "INICIAR PARTIDA/BOTON_PERSONALIZAR"), true);
            Corner(SkinButton("Volver", "INICIAR PARTIDA/BOTON_REGRESAR"), false);
            // El código va debajo del banner PARTIDA (antes quedaba tapado por él).
            if (lobby.CodeText != null)
            {
                Layout(lobby.CodeText.transform, TopCenter, new Vector2(0, -155), new Vector2(600, 50));
                Style(lobby.CodeText, regular, 30, Color.white, TextAnchor.MiddleCenter);
            }
            // Tarjetas un poco más abajo para que no las tape el código.
            float[] slotX = { -435, -145, 145, 435 };
            for (int i = 1; i <= 4; i++)
            {
                Transform slot = Named("Jugador" + i);
                if (slot == null) { Warn("No se encontró Jugador" + i); continue; }
                SetSprite(slot, "INICIAR PARTIDA/MARCO_JUGADOR", false);
                Layout(slot, Center, new Vector2(slotX[i - 1], -20), new Vector2(235, 325));
            }
            // Puesto vacío: el ratón de frente en gris, dentro del círculo.
            if (lobby.Keys != null)
                foreach (Image key in lobby.Keys)
                    if (key != null)
                    {
                        SetSprite(key.transform, "PODIO/raton_frente");
                        Layout(key.transform, Center, new Vector2(0, 37), new Vector2(68, 60));
                    }
            Undo.RecordObject(lobby, "Ratón de frente"); lobby.MouseSprite = LoadSprite("PODIO/raton_frente");
            LobbyPreviews(lobby);
            if (lobby.CodeText != null)
                Layout(Decoration("TituloPartida", "INICIAR PARTIDA/TituloPartida", lobby.CodeText.transform.parent, 0), TopCenter, new Vector2(0, -70), new Vector2(430, 122));
            HostLabels(lobby);
            Undo.RecordObject(lobby, "Auras"); lobby.AuraSprites = Auras(); lobby.KeySprites = KeySprites();
        }

        // Un ratón 3D girando por puesto (MousePreview), del tamaño del círculo del aura.
        static void LobbyPreviews(LobbyUI lobby)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<PlayerView>(PlayerPrefab);
            if (prefab == null || lobby.Auras == null) { Warn("No se pudo preparar el ratón 3D del lobby."); return; }
            var previews = new MousePreview[lobby.Auras.Length];
            for (int i = 0; i < previews.Length; i++)
            {
                Image aura = lobby.Auras[i]; if (aura == null) continue;
                Transform slot = aura.transform.parent;
                Transform existing = slot.Find("VistaRaton");
                GameObject go;
                if (existing != null) go = existing.gameObject;
                else
                {
                    go = new GameObject("VistaRaton", typeof(RectTransform), typeof(RawImage), typeof(MousePreview));
                    Undo.RegisterCreatedObjectUndo(go, "Crear ratón del lobby");
                    go.transform.SetParent(slot, false);
                }
                go.transform.SetSiblingIndex(aura.transform.GetSiblingIndex() + 1);
                Layout(go.transform, Center, new Vector2(0, 40), new Vector2(120, 120));
                RawImage raw = go.GetComponent<RawImage>();
                Undo.RecordObject(raw, "Ratón del lobby"); raw.texture = null; raw.color = Color.clear; raw.raycastTarget = false;
                MousePreview preview = go.GetComponent<MousePreview>();
                Undo.RecordObject(preview, "Ratón del lobby");
                preview.Prefab = prefab; preview.Target = raw; preview.Resolution = 256;
                // Más alto y más lejos que en Personalizar para que se vea el aura completa debajo.
                preview.CameraPosition = new Vector3(0, 2.6f, 4.6f); preview.LookHeight = .7f;
                // Empieza oculto: LobbyUI lo muestra cuando el puesto tiene jugador.
                Hide(go);
                previews[i] = preview;
            }
            Undo.RecordObject(lobby, "Ratón del lobby"); lobby.Previews = previews;
        }

        // Nombre del jugador en Thin y, debajo, "Anfitrión" en Bold (un Text por fuente: Unity no mezcla fuentes en uno).
        static void HostLabels(LobbyUI lobby)
        {
            if (lobby.Names == null) return;
            var hosts = new Text[lobby.Names.Length];
            for (int i = 0; i < lobby.Names.Length; i++)
            {
                Text name = lobby.Names[i]; if (name == null) continue;
                Transform existing = name.transform.parent.Find("Anfitrion");
                Text host = existing != null ? existing.GetComponent<Text>() : null;
                if (host == null)
                {
                    GameObject copy = Object.Instantiate(name.gameObject, name.transform.parent, false);
                    Undo.RegisterCreatedObjectUndo(copy, "Crear Anfitrión");
                    copy.name = "Anfitrion"; host = copy.GetComponent<Text>(); host.text = "";
                }
                Layout(name.transform, Center, new Vector2(0, -80), new Vector2(215, 60));
                Layout(host.transform, Center, new Vector2(0, -132), new Vector2(215, 32));
                Style(host, bold, 22, name.color, TextAnchor.MiddleCenter);
                hosts[i] = host;
            }
            Undo.RecordObject(lobby, "Anfitrión"); lobby.HostLabels = hosts;
        }

        static void Personalize()
        {
            PersonalizeUI ui = Find<PersonalizeUI>(); if (ui == null) { Warn("Falta PersonalizeUI."); return; }
            Transform panel = Named("ControlesPersonalizar");
            if (panel == null) Warn("No se encontró ControlesPersonalizar.");
            else
            {
                if (SetSprite(panel, "PERSONALIZAR/MARCO_PERSONALIZAR", false) != null) HideText("PERSONALIZAR");
                // Pegado a la derecha para dejarle espacio al ratón.
                Layout(panel, Center, new Vector2(300, 0), new Vector2(580, 580));
            }
            if (ui.Preview != null)
            {
                var view = (RectTransform)ui.Preview.transform;
                Undo.RecordObject(view, "Acomodar ratón");
                view.anchorMin = new Vector2(.02f, .1f); view.anchorMax = new Vector2(.5f, .9f); view.offsetMin = view.offsetMax = Vector2.zero;
                // Sin textura el RawImage se ve como un rectángulo blanco: transparente hasta que MousePreview
                // le pone la imagen del ratón (fondo transparente), así se ve FONDO_PERSONALIZAR detrás.
                RawImage raw = ui.Preview.GetComponent<RawImage>();
                if (raw != null) { Undo.RecordObject(raw, "Vista del ratón"); raw.texture = null; raw.color = Color.clear; }
            }
            SkinButton("LLAVE", "PERSONALIZAR/LLAVE_PERSONALIZAR");
            SkinButton("AURA", "PERSONALIZAR/AURA_PERSONALIZAR");
            SkinButton("Guardar", "PERSONALIZAR/BOTON_GUARDAR");
            Corner(SkinButton("Volver", "PERSONALIZAR/BOTON_REGRESAR"), false);
            Sprite[] keys = KeySprites();
            Undo.RecordObject(ui, "Auras"); ui.AuraSprites = Auras(); ui.KeySprites = keys;
            // Las llaves ya se ven en la escena: vista previa (amarilla, la de por defecto) y un botón por color.
            if (ui.KeyPreview != null) SetSprite(ui.KeyPreview.transform, "LLAVES/LLAVE_AMARILLA");
            for (int i = 0; i < keys.Length; i++)
            {
                Transform color = Named("Color" + i);
                if (color == null) Warn("No se encontró Color" + i); else SetSprite(color, "LLAVES/LLAVE_" + AuraNames[i]);
            }
        }

        static void Settings()
        {
            SettingsUI ui = Find<SettingsUI>(); if (ui == null) { Warn("Falta SettingsUI."); return; }
            Corner(SkinButton("Volver", "CONFIGURACION/BOTON_REGRESAR"), false);
            // Volver al menú guarda las preferencias (SettingsUI.Back): va sobre el borde inferior del marco.
            Layout(SkinButton("Volver al menú", "CONFIGURACION/BOTON_GUARDAR"), Center, new Vector2(0, -160), new Vector2(335, 62));

            Text title = FindText("CONFIGURACIÓN");
            Transform parent = title != null ? title.transform.parent : ui.Music != null ? ui.Music.transform.parent : null;
            if (parent != null)
                Layout(Decoration("MarcoConfiguracion", "CONFIGURACION/MARCO_CONFIGURACION", parent, 0, preserveAspect: false), Center, new Vector2(0, 20), new Vector2(640, 362));
            if (title != null) Hide(title.gameObject);

            SliderRow(ui.Music, 42, "ICONO_MUSICA", "IconoMusica", FindText("Música"));
            SliderRow(ui.Effects, -48, "ICONO_SONIDO", "IconoEfectos", FindText("Efectos de sonido"));
            if (ui.MusicValue != null) Hide(ui.MusicValue.gameObject);
            if (ui.EffectsValue != null) Hide(ui.EffectsValue.gameObject);
        }
        // Alto de la barra de madera y grosor del borde de madera que queda visible alrededor del interior.
        const float SliderHeight = 58, SliderInset = 11, HandleTravel = 16;
        static void SliderRow(Slider slider, float y, string iconFile, string iconName, Text label)
        {
            if (slider == null) { Warn("Falta un slider en SettingsUI."); return; }
            Layout(slider.transform, Center, new Vector2(40, y), new Vector2(400, SliderHeight));
            // Tres píldoras concéntricas: borde de madera (Pista) a todo el alto, interior gris claro y relleno oscuro.
            Transform track = Named("Pista", slider.transform);
            if (track != null)
            {
                SetSprite(track, "CONFIGURACION/FONDO_SLIDER", false);
                WoodTrack(track);
                Transform interior = Child(slider.transform, "Interior", track.GetSiblingIndex() + 1);
                Pill(interior, "CONFIGURACION/PILDORA_SLIDER", new Color(.85f, .85f, .85f), SliderHeight - 2 * SliderInset);
                interior.GetComponent<Image>().raycastTarget = false;
                Stretch(interior, new Vector2(SliderInset, SliderInset));
            }
            else Warn("Falta Pista en " + slider.name);
            SliderFill(slider);
            if (slider.handleRect != null)
            {
                SetSprite(slider.handleRect, "CONFIGURACION/CONTROL_SLIDER");
                RectTransform handle = slider.handleRect;
                Undo.RecordObject(handle, "Manija");
                handle.anchorMin = new Vector2(handle.anchorMin.x, .5f); handle.anchorMax = new Vector2(handle.anchorMax.x, .5f);
                handle.sizeDelta = new Vector2(52, SliderHeight + 8);
                // La manija recorre todo el interior sin salirse de los extremos redondeados.
                var area = (RectTransform)handle.parent;
                Undo.RecordObject(area, "Area manija");
                area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
                area.offsetMin = new Vector2(SliderInset + HandleTravel, 0); area.offsetMax = new Vector2(-SliderInset - HandleTravel, 0);
                area.SetAsLastSibling();
            }
            else Warn("Falta Manija en " + slider.name);

            // Ícono a la izquierda; si Lowell aún no lo sube, queda el texto en su lugar.
            Transform parent = slider.transform.parent;
            if (AssetDatabase.LoadAssetAtPath<Sprite>(Art + "CONFIGURACION/" + iconFile + ".png") != null)
            {
                Layout(Decoration(iconName, "CONFIGURACION/" + iconFile, parent, slider.transform.GetSiblingIndex()), Center, new Vector2(-222, y), new Vector2(64, 64));
                if (label != null) Hide(label.gameObject);
            }
            else
            {
                Warn("Falta " + Art + "CONFIGURACION/" + iconFile + ".png; se deja el texto.");
                if (label != null)
                {
                    Layout(label.transform, Center, new Vector2(-245, y), new Vector2(120, 36));
                    Style(label, regular, 18, Dark, TextAnchor.MiddleRight);
                }
            }
        }
        // FONDO_SLIDER (425x43) trae su sombra dibujada: la madera ocupa x 4–421 y las 35 filas de arriba.
        // La pista se agranda para que la madera (sin sombra) mida justo el slider y el interior quede centrado.
        static void WoodTrack(Transform track)
        {
            Image image = track.GetComponent<Image>(); if (image == null) return;
            float scale = SliderHeight / 35f;
            Undo.RecordObject(image, "Madera");
            image.type = Image.Type.Sliced; image.fillCenter = true; image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 100 / (scale * image.sprite.pixelsPerUnit);
            Stretch(track, Vector2.zero);
            var rect = (RectTransform)track;
            rect.offsetMin = new Vector2(-4 * scale, -8 * scale); rect.offsetMax = new Vector2(4 * scale, 0);
            // La sombra ya viene en la imagen; quita la que se agregaba antes.
            Shadow shadow = track.GetComponent<Shadow>();
            if (shadow != null) Undo.DestroyObjectImmediate(shadow);
        }
        // Relleno gris oscuro a la izquierda de la manija, como en el diseño.
        static void SliderFill(Slider slider)
        {
            Transform interior = slider.transform.Find("Interior");
            Transform area = slider.transform.Find("Area relleno");
            if (area == null)
            {
                var go = new GameObject("Area relleno", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Crear relleno");
                go.transform.SetParent(slider.transform, false);
                area = go.transform;
            }
            area.SetSiblingIndex(interior != null ? interior.GetSiblingIndex() + 1 : 0);
            // El relleno termina HandleTravel px después del valor y la manija empieza HandleTravel px adentro:
            // así su punta queda bajo la manija y al 100 % llega justo al borde del interior.
            Stretch(area, new Vector2(SliderInset, SliderInset));
            var areaRect = (RectTransform)area;
            areaRect.offsetMax = new Vector2(-SliderInset - HandleTravel, -SliderInset);
            Transform fill = area.Find("Relleno");
            if (fill == null)
            {
                var go = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Crear relleno");
                go.transform.SetParent(area, false);
                fill = go.transform;
            }
            var fillRect = (RectTransform)fill;
            Undo.RecordObject(fillRect, "Relleno");
            // Se pasa un poco del valor para que su punta redonda quede bajo la manija.
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = new Vector2(0, 1);
            fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = new Vector2(HandleTravel, 0);
            Pill(fill, "CONFIGURACION/PILDORA_SLIDER", new Color(.243f, .294f, .322f), SliderHeight - 2 * SliderInset);
            fill.GetComponent<Image>().raycastTarget = false;
            Undo.RecordObject(slider, "Relleno"); slider.fillRect = fillRect;
        }
        // Hijo con Image (se crea una vez) en la posición indicada.
        static Transform Child(Transform parent, string name, int siblingIndex)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
                go.transform.SetParent(parent, false);
                child = go.transform;
            }
            child.SetSiblingIndex(siblingIndex);
            return child;
        }
        // Ocupa todo el padre dejando un margen.
        static void Stretch(Transform target, Vector2 inset)
        {
            if (target == null) return;
            var rect = (RectTransform)target;
            Undo.RecordObject(rect, "Acomodar UI");
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = inset; rect.offsetMax = -inset;
        }
        // Píldora 9-slice con extremos de radio = mitad del alto (PILDORA_SLIDER blanca teñida, o FONDO_SLIDER).
        static void Pill(Transform target, string sprite, Color color, float height)
        {
            Image image = target.GetComponent<Image>();
            if (image == null) return;
            Sprite pill = LoadSprite(sprite); if (pill == null) return;
            Undo.RecordObject(image, "Pildora");
            image.sprite = pill; image.type = Image.Type.Sliced; image.fillCenter = true; image.preserveAspect = false;
            // El borde del sprite (en px) debe medir height/2 en la UI (referencia de 100 px por unidad).
            image.pixelsPerUnitMultiplier = pill.border.x / (height / 2) * 100 / pill.pixelsPerUnit;
            image.color = color;
        }

        static void Game()
        {
            GameUI game = Find<GameUI>(); if (game == null) { Warn("Falta GameUI."); return; }
            // Laterales: RawImage con los azulejos a tamaño real (256x720 de 1920x1080).
            Tiles("FondoLateralIzquierdo", 0);
            Tiles("FondoLateralDerecho", .5f);
            Corner(SkinButton("Volver", "JUGAR/BOTON_REGRESAR"), false);

            Stick(game);
            ExitDialog(game);

            ParticipantBar(game);

            if (game.Timer != null)
            {
                var timer = (RectTransform)game.Timer.transform;
                Image frame = Decoration("MarcoTiempo", "JUGAR/TIEMPO MARCO", timer.parent, timer.GetSiblingIndex(), preserveAspect: false);
                Layout(frame, TopRight, new Vector2(-115, -45), new Vector2(140, 46));
                Layout(timer, TopRight, new Vector2(-115, -45), new Vector2(140, 46));
                Style(game.Timer, bold, 26, Dark, TextAnchor.MiddleCenter);
            }

            Power(game.SpeedButton, "JUGAR/POWERUP_VELOCIDAD", -258, game.SpeedLabel);
            Power(game.FreezeButton, "JUGAR/POWERUP_DETENER", -486, game.FreezeLabel);
            // Puntos y alimentos propios en el panel izquierdo, entre regresar y la cruceta.
            if (game.OwnScore != null)
            {
                Show(game.OwnScore.gameObject);
                Layout(game.OwnScore.transform, TopLeft, new Vector2(128, -205), new Vector2(240, 60));
                Style(game.OwnScore, thin, 22, Dark, TextAnchor.MiddleCenter);
            }
            // "¡DETENIDO!" grande al centro del mapa.
            if (game.Inventory != null)
            {
                Layout(game.Inventory.transform, Center, Vector2.zero, new Vector2(900, 120));
                // Crema con borde café, como el título de "¿Salir de la partida?" de antes.
                Style(game.Inventory, bold, 64, Cream, TextAnchor.MiddleCenter);
                Outline outline = game.Inventory.GetComponent<Outline>() ?? Undo.AddComponent<Outline>(game.Inventory.gameObject);
                Undo.RecordObject(outline, "Borde detenido"); outline.effectColor = Dark; outline.effectDistance = new Vector2(4, -4);
                Undo.RecordObject(game.Inventory, "Aviso detenido"); game.Inventory.raycastTarget = false;
            }
        }
        // "¿Salir de la partida?": marco de madera (MARCO_CONFIGURACION con la píldora del título en blanco), el título
        // café sobre la píldora como "CONFIGURACIÓN" y los dos botones como píldoras amarillas de queso.
        // El selector de trampa usa el mismo marco y su título va igual.
        static readonly Color Cheese = new Color(1, .78f, .25f);
        static void ExitDialog(GameUI game)
        {
            if (game.ExitDialog == null) { Warn("GameUI no tiene ExitDialog."); return; }
            Transform panel = Named("Panel", game.ExitDialog.transform);
            if (panel == null) { Warn("No se encontró el Panel del diálogo de salida."); return; }
            if (SetSprite(panel, "CONFIGURACION/MARCO_DIALOGO", false) == null) return;
            Layout(panel, Center, Vector2.zero, new Vector2(640, 364));
            Text title = null, message = null;
            foreach (Text text in panel.GetComponentsInChildren<Text>(true))
            {
                if (text.GetComponentInParent<Button>(true) != null) continue;
                if (title == null) title = text; else if (message == null) message = text;
            }
            if (title != null)
            {
                // La píldora ocupa el 52 % del ancho del marco, centrada a 49 px (de 470) del borde superior.
                Layout(title.transform, TopCenter, new Vector2(0, -39), new Vector2(340, 50));
                Style(title, bold, 24, Dark, TextAnchor.MiddleCenter);
                RemoveOutline(title);
            }
            if (game.FreezePicker != null) FreezePicker(game.FreezePicker);
            if (message != null)
            {
                Layout(message.transform, Center, new Vector2(0, 20), new Vector2(540, 60));
                Style(message, regular, 24, Dark, TextAnchor.MiddleCenter);
            }
            float x = -135;
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                Layout(button, Center, new Vector2(x, -85), new Vector2(230, 62)); x = -x;
                Pill(button.transform, "CONFIGURACION/PILDORA_SLIDER", Cheese, 62);
                Undo.RecordObject(button, "Botón diálogo");
                ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, .95f, .85f);
                colors.pressedColor = new Color(.85f, .75f, .6f); button.colors = colors;
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null) { Show(label.gameObject); Layout(label.transform, Center, Vector2.zero, new Vector2(220, 56)); Style(label, bold, 24, Dark, TextAnchor.MiddleCenter); }
            }
        }
        // Selector de trampa: en un panel de 600 px de alto la píldora del título termina a unos 120 px del borde,
        // así que la indicación va debajo de ella y los botones bajan detrás (el último queda sobre el borde de cuadros).
        static void FreezePicker(FreezeTargetPicker picker)
        {
            if (picker.Panel != null) Layout(picker.Panel, Center, Vector2.zero, new Vector2(600, 600));
            if (picker.Title != null)
            {
                Layout(picker.Title, TopCenter, new Vector2(0, -62), new Vector2(340, 50));
                Style(picker.Title, bold, 28, Dark, TextAnchor.MiddleCenter); RemoveOutline(picker.Title);
            }
            if (picker.Hint != null) Layout(picker.Hint, TopCenter, new Vector2(0, -158), new Vector2(500, 52));
            float y = -218;
            foreach (Button button in picker.RivalButtons ?? new Button[0])
                if (button != null) { Layout(button, TopCenter, new Vector2(0, y), new Vector2(484, 60)); y -= 66; }
            if (picker.AllButton != null) Layout(picker.AllButton, TopCenter, new Vector2(0, -422), new Vector2(484, 60));
            if (picker.CancelButton != null) Layout(picker.CancelButton, TopCenter, new Vector2(0, -494), new Vector2(484, 60));
        }
        static void RemoveOutline(Text text)
        {
            Outline outline = text.GetComponent<Outline>();
            if (outline != null) Undo.DestroyObjectImmediate(outline);
        }
        // Joystick abajo a la izquierda: base redonda y el queso como perilla (sin las flechas de antes).
        static void Stick(GameUI game)
        {
            Transform pad = Named("Cruceta");
            if (pad == null) { Warn("No se encontró Cruceta."); return; }
            foreach (string arrow in new[] { "Arriba", "Abajo", "Izq.", "Der." })
            {
                Transform old = Named(arrow, pad);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }
            Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Ratones/Arte/Circulo.png");
            if (circle == null) { Warn("No se encontró el sprite Assets/Ratones/Arte/Circulo.png"); return; }
            Image ring = pad.GetComponent<Image>();
            if (ring == null) ring = Undo.AddComponent<Image>(pad.gameObject);
            Undo.RecordObject(ring, "Base joystick");
            ring.sprite = circle; ring.type = Image.Type.Simple; ring.preserveAspect = true;
            ring.color = new Color(Dark.r, Dark.g, Dark.b, .35f); ring.raycastTarget = true;
            Layout(pad, BottomLeft, new Vector2(140, 285), new Vector2(230, 230));

            Transform knob = Child(pad, "Perilla", pad.childCount);
            SetSprite(knob, "JUGAR/FONDO_CRUZETA");
            knob.GetComponent<Image>().raycastTarget = false;
            Layout(knob, Center, Vector2.zero, new Vector2(130, 130));

            Joystick stick = pad.GetComponent<Joystick>();
            if (stick == null) stick = Undo.AddComponent<Joystick>(pad.gameObject);
            Undo.RecordObject(stick, "Joystick");
            stick.Knob = (RectTransform)knob; stick.Radius = 70;
            Undo.RecordObject(game, "Joystick"); game.Stick = stick;
        }
        static void Tiles(string name, float offsetX)
        {
            Transform t = Named(name);
            RawImage raw = t != null ? t.GetComponent<RawImage>() : null;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "JUGAR/FONDO_JUGAR.png");
            if (raw == null || texture == null) { Warn("No se pudo poner FONDO_JUGAR en " + name); return; }
            Undo.RecordObject(raw, "Azulejos");
            raw.texture = texture; raw.color = Color.white;
            raw.uvRect = new Rect(offsetX, 0, .2f * 1280 / texture.width, 720f / texture.height);
        }
        // Un marcador de madera por jugador arriba del mapa, con nombre y puntos (el acomodo original de la escena).
        static void ParticipantBar(GameUI game)
        {
            if (game.PlayerLabels == null || game.PlayerLabels.Length == 0) { Warn("GameUI no tiene PlayerLabels."); return; }
            Transform map = game.PlayerLabels.Where(l => l != null).Select(l => l.transform.parent.parent).FirstOrDefault();
            // Quita la barra única y las auras de una versión anterior.
            Transform bar = map != null ? map.Find("BarraParticipantes") : null;
            if (bar != null) Undo.DestroyObjectImmediate(bar.gameObject);
            for (int i = 0; i < game.PlayerLabels.Length; i++)
            {
                Text label = game.PlayerLabels[i]; if (label == null) continue;
                Transform marker = label.transform.parent;
                Transform aura = marker.Find("Aura");
                if (aura != null) Undo.DestroyObjectImmediate(aura.gameObject);
                SetSprite(marker, "JUGAR/BARRA PARTICIPANTES", false);
                Layout(marker, TopCenter, new Vector2(-276 + 184 * i, -52), new Vector2(174, 72));
                Layout(label.transform, Center, Vector2.zero, new Vector2(169, 68));
                Style(label, regular, 20, new Color(.12f, .12f, .12f), TextAnchor.MiddleCenter);
            }
            Undo.RecordObject(game, "Marcadores");
            game.PlayerAuras = new Image[0];
        }
        static void Power(Button button, string sprite, float y, Text label)
        {
            if (button == null) { Warn("Falta un botón de poder en GameUI."); return; }
            SetSprite(button.transform, sprite);
            Layout(button.transform, TopRight, new Vector2(-137, y), new Vector2(200, 130));
            Undo.RecordObject(button, "Poder");
            // Sin carga el poder se ve gris.
            ColorBlock colors = button.colors; colors.disabledColor = new Color(.5f, .5f, .5f, 1); button.colors = colors;
            if (label == null) return;
            // "VELOCIDAD / Sin carga" encima de la imagen.
            Layout(label.transform, TopCenter, new Vector2(0, 26), new Vector2(220, 50));
            Style(label, regular, 20, Dark, TextAnchor.MiddleCenter);
        }

        static void Results()
        {
            ResultsUI ui = Find<ResultsUI>(); if (ui == null) { Warn("Falta ResultsUI."); return; }
            Corner(SkinButton("Salir al menú", "PODIO/BOTON_MENU"), false);
            Corner(SkinButton("X", "PODIO/BOTON_CERRAR"), true);
            if (ui.Title != null)
            {
                Layout(Decoration("TituloFelicidades", "PODIO/TITULO_FELICIDADES", ui.Title.transform.parent, ui.Title.transform.GetSiblingIndex()),
                    TopCenter, new Vector2(0, -115), new Vector2(540, 160));
                // El banner ya dice ¡FELICIDADES!; el texto del resultado no va en el diseño.
                Hide(ui.Title.gameObject);
            }
            Podium(ui);
            if (ui.Fourth != null)
            {
                // Blanco suave: va sobre el piso oscuro de FONDO_PODIO.
                Layout(ui.Fourth.transform, Center, new Vector2(20, -296), new Vector2(400, 48));
                Style(ui.Fourth, bold, 30, new Color(.94f, .94f, .94f), TextAnchor.MiddleCenter);
                var fitter = ui.Fourth.GetComponent<ContentSizeFitter>() ?? Undo.AddComponent<ContentSizeFitter>(ui.Fourth.gameObject);
                Undo.RecordObject(fitter, "Ajustar 4.º"); fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                Image medal = Decoration("Medalla", "PODIO/MEDALLA_ULTIMOPUESTO", ui.Fourth.transform, 0);
                Layout(medal, LeftMiddle, new Vector2(-26, 0), new Vector2(40, 40));
                if (medal != null) { Undo.RecordObject(ui, "Medalla"); ui.FourthMedal = medal.gameObject; }
            }
            Undo.RecordObject(ui, "Auras"); ui.AuraSprites = Auras();
            Confetti(ui);
        }
        // Una sola imagen PODIO; cada puesto (1.º, 2.º, 3.º) se para sobre su escalón con aura, puntos y nombre.
        static void Podium(ResultsUI ui)
        {
            var podiums = ui.Podiums ?? new GameObject[0];
            if (podiums.Length == 0 || podiums[0] == null) { Warn("ResultsUI no tiene Podiums asignados."); return; }
            Transform parent = podiums[0].transform.parent;
            int index = podiums.Where(p => p != null && p.transform.parent == parent).Min(p => p.transform.GetSiblingIndex());
            Layout(Decoration("Podio", "PODIO/PODIO", parent, index), Center, new Vector2(0, -105), new Vector2(661, 293));
            // Centro y altura de cada escalón sobre la imagen del podio (orden lógico 1, 2, 3).
            Vector2[] steps = { new Vector2(0, 36), new Vector2(-214, -46), new Vector2(218, -80) };
            var prefab = AssetDatabase.LoadAssetAtPath<PlayerView>(PlayerPrefab);
            if (prefab == null) Warn("No se encontró " + PlayerPrefab + "; el podio queda sin ratones 3D.");
            var previews = new MousePreview[podiums.Length];
            for (int i = 0; i < podiums.Length && i < steps.Length; i++)
            {
                GameObject podium = podiums[i]; if (podium == null) continue;
                Layout(podium.transform, Center, steps[i], new Vector2(200, 140), BottomCenter);
                Transform plain = Named("BasePodio", podium.transform);
                if (plain != null) Hide(plain.gameObject);
                if (At(ui.Colors, i) != null)
                {
                    Image aura = ui.Colors[i];
                    if (aura.transform.parent != podium.transform) Undo.SetTransformParent(aura.transform, podium.transform, "Aura");
                    Layout(aura.transform, BottomCenter, new Vector2(0, 28), new Vector2(48, 48));
                }
                // Con ratón 3D (parado sobre el escalón) los textos suben por encima de él.
                bool mouse = prefab != null && (previews[i] = PodiumPreview(podium.transform, prefab)) != null;
                Stack(At(ui.Scores, i), podium.transform, mouse ? 74 : 62, thin, 18);
                Stack(At(ui.Names, i), podium.transform, mouse ? 100 : 90, regular, 22);
                if (At(ui.Places, i) != null) Hide(ui.Places[i].gameObject);
            }
            Undo.RecordObject(ui, "Ratones del podio"); ui.Previews = previews;
        }
        static MousePreview PodiumPreview(Transform podium, PlayerView prefab)
        {
            Transform existing = podium.Find("VistaRaton");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("VistaRaton", typeof(RectTransform), typeof(RawImage), typeof(MousePreview));
                Undo.RegisterCreatedObjectUndo(go, "Crear ratón del podio");
                go.transform.SetParent(podium, false);
            }
            // Más abajo que el escalón: el render deja aire bajo el aura y así el ratón se para sobre el mantel.
            Layout(go.transform, BottomCenter, new Vector2(0, -36), new Vector2(110, 100), BottomCenter);
            RawImage raw = go.GetComponent<RawImage>();
            Undo.RecordObject(raw, "Ratón del podio"); raw.texture = null; raw.color = Color.clear; raw.raycastTarget = false;
            MousePreview preview = go.GetComponent<MousePreview>();
            Undo.RecordObject(preview, "Ratón del podio");
            preview.Prefab = prefab; preview.Target = raw; preview.Resolution = 256;
            // Mismo encuadre que en el lobby: el ratón completo con su aura debajo.
            preview.CameraPosition = new Vector3(0, 2.6f, 4.6f); preview.LookHeight = .7f;
            Show(go);
            return preview;
        }
        static void Stack(Text text, Transform podium, float y, Font font, int size)
        {
            if (text == null) return;
            if (text.transform.parent != podium) Undo.SetTransformParent(text.transform, podium, "Puesto");
            Layout(text.transform, BottomCenter, new Vector2(0, y), new Vector2(220, 30));
            // Blanco: va sobre la pared gris de FONDO_PODIO.
            Style(text, font, size, Color.white, TextAnchor.MiddleCenter);
        }
        static T At<T>(T[] items, int i) where T : Object { return items != null && i < items.Length ? items[i] : null; }

        // Cuadros extraídos de PODIO/AnimacionCofetti.gif (Unity no anima GIF), encima de todo el Canvas.
        static void Confetti(ResultsUI ui)
        {
            Sprite[] frames = AssetDatabase.FindAssets("t:Sprite", new[] { Art + "PODIO/Confeti" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
            if (frames.Length == 0) { Warn("No hay cuadros en " + Art + "PODIO/Confeti; se omite el confeti."); return; }
            Canvas canvas = ui.GetComponentInParent<Canvas>() ?? Find<Canvas>();
            if (canvas == null) { Warn("Falta el Canvas para el confeti."); return; }
            Transform existing = canvas.transform.Find("Confeti");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("Confeti", typeof(RectTransform), typeof(Image), typeof(SpriteAnimation));
                Undo.RegisterCreatedObjectUndo(go, "Crear confeti");
                go.transform.SetParent(canvas.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            go.transform.SetAsLastSibling();
            Image image = go.GetComponent<Image>();
            Undo.RecordObject(image, "Confeti");
            image.sprite = frames[0]; image.raycastTarget = false; image.preserveAspect = false; image.color = Color.white;
            SpriteAnimation animation = go.GetComponent<SpriteAnimation>() ?? Undo.AddComponent<SpriteAnimation>(go);
            Undo.RecordObject(animation, "Confeti");
            animation.Frames = frames; animation.Fps = ConfettiFps; animation.Loop = false; animation.Repeats = ConfettiRepeats;
        }

        // ---------- Utilidades ----------

        static Sprite[] Auras()
        {
            var sprites = new Sprite[AuraNames.Length];
            for (int i = 0; i < sprites.Length; i++) sprites[i] = LoadSprite("AURAS/AURA_" + AuraNames[i]);
            return sprites;
        }
        static Sprite[] KeySprites()
        {
            var sprites = new Sprite[AuraNames.Length];
            for (int i = 0; i < sprites.Length; i++) sprites[i] = LoadSprite("LLAVES/LLAVE_" + AuraNames[i]);
            return sprites;
        }
        static Sprite LoadSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
            if (sprite == null) Warn("No se encontró el sprite " + Art + name + ".png");
            return sprite;
        }
        static Button SkinButton(string name, string sprite, bool optional = false, Transform under = null)
        {
            IEnumerable<Button> buttons = under != null ? under.GetComponentsInChildren<Button>(true) : All<Button>();
            Button button = buttons.FirstOrDefault(b => b.name == name);
            if (button == null) { if (!optional) Warn("No se encontró el botón " + name); return null; }
            if (SetSprite(button.transform, sprite) == null) return button;
            // El texto ya viene dibujado en la imagen.
            foreach (Text label in button.GetComponentsInChildren<Text>(true)) if (label.transform != button.transform) Hide(label.gameObject);
            return button;
        }
        static Image SetSprite(Transform target, string name, bool preserveAspect = true)
        {
            Sprite sprite = LoadSprite(name); if (sprite == null) return null;
            Image image = target.GetComponent<Image>();
            if (image == null)
            {
                if (target.GetComponent<Graphic>() != null) { Warn(target.name + " ya tiene otro Graphic; no se puede poner " + name); return null; }
                image = Undo.AddComponent<Image>(target.gameObject);
            }
            Undo.RecordObject(image, "Sprite UI nueva");
            image.sprite = sprite; image.type = Image.Type.Simple; image.preserveAspect = preserveAspect; image.color = Color.white;
            return image;
        }
        // Imagen decorativa (marco, banner) que se crea una vez y se reutiliza al volver a aplicar.
        static Image Decoration(string name, string sprite, Transform parent, int siblingIndex, bool preserveAspect = true)
        {
            if (parent == null) { Warn("Sin padre para " + name); return null; }
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
                go.transform.SetParent(parent, false);
                go.transform.SetSiblingIndex(siblingIndex);
            }
            Image image = SetSprite(go.transform, sprite, preserveAspect);
            if (image != null) image.raycastTarget = false;
            return image;
        }
        // Botones redondos de las esquinas superiores (regresar, personalizar, cerrar…): grandes y a 20 px del borde.
        const float CornerSize = 130, CornerOffset = 85;
        static void Corner(Component button, bool right)
        {
            Layout(button, right ? TopRight : TopLeft, new Vector2(right ? -CornerOffset : CornerOffset, -CornerOffset), new Vector2(CornerSize, CornerSize));
        }
        static void Layout(Component target, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            if (target == null) return;
            var rect = (RectTransform)target.transform;
            Undo.RecordObject(rect, "Acomodar UI");
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot ?? new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        static void Style(Text text, Font font, int size, Color color, TextAnchor alignment)
        {
            Undo.RecordObject(text, "Estilo de texto");
            text.font = font; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        }
        static Text FindText(string value)
        {
            return All<Text>().FirstOrDefault(t => t.GetComponentInParent<Selectable>(true) == null &&
                string.Equals((t.text ?? "").Trim(), value, System.StringComparison.OrdinalIgnoreCase));
        }
        static void HideText(string value)
        {
            foreach (Text text in All<Text>())
                if (text.GetComponentInParent<Selectable>(true) == null && string.Equals((text.text ?? "").Trim(), value, System.StringComparison.OrdinalIgnoreCase))
                    Hide(text.gameObject);
        }
        static void Show(GameObject go)
        {
            if (go.activeSelf) return;
            Undo.RecordObject(go, "Mostrar"); go.SetActive(true);
        }
        static void Hide(GameObject go)
        {
            if (!go.activeSelf) return;
            Undo.RecordObject(go, "Ocultar"); go.SetActive(false);
        }
        static IEnumerable<T> All<T>() where T : Component
        { return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)); }
        static T Find<T>() where T : Component { return All<T>().FirstOrDefault(); }
        static Transform Named(string name, Transform under = null)
        {
            IEnumerable<Transform> all = under != null ? under.GetComponentsInChildren<Transform>(true) : All<Transform>();
            return all.FirstOrDefault(t => t.name == name);
        }
        static void Warn(string message) { warnings++; Debug.LogWarning(scene.name + ": " + message); }
    }
}

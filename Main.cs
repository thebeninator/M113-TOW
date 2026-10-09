using System.Linq;
using MelonLoader;
using UnityEngine;
using GHPC.State;
using System.Collections;
using GHPC.Audio;
using GHPC.Player;
using GHPC.Camera;
using GHPC.Vehicle;
using ModUtil;
using M113Tow;

[assembly: MelonInfo(typeof(Mod), "M113 TOW", "1.0.5", "ATLAS")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace M113Tow
{
    public class Mod : MelonMod
    {
        private static ModuleManager module_manager;
        internal static Vehicle[] vics;
        internal static MelonPreferences_Category cfg;

        private GameObject game_manager;
        internal static AudioSettingsManager audio_settings_manager;
        internal static PlayerInput player_manager;
        internal static CameraManager camera_manager;
        private int valid_scene_count = 0;

        public IEnumerator OnPlayerReady(GameState _)
        {
            game_manager = GameObject.Find("_APP_GHPC_");
            audio_settings_manager = game_manager.GetComponent<AudioSettingsManager>();
            player_manager = game_manager.GetComponent<PlayerInput>();
            camera_manager = game_manager.GetComponent<CameraManager>();
            vics = GameObject.FindObjectsByType<Vehicle>(FindObjectsSortMode.None);

            yield break;
        }

        public override void OnInitializeMelon()
        {
            module_manager = new ModuleManager("M113TOW");
            cfg = MelonPreferences.CreateCategory("M113TOW");
            M113Tow.Config(cfg);

            module_manager.Add("M113TOW", new M113Tow());
        }

        public override void OnSceneWasLoaded(int build_idx, string scene_name)
        {
            if (scene_name == "MainMenu2_Scene" || scene_name == "MainMenu2-1_Scene" || scene_name == "t64_menu")
            {
                module_manager.LoadAllStaticAssets();
            }

            if (Util.menu_screens.Contains(scene_name)) return;

            valid_scene_count++;

            if (valid_scene_count == 1)
            {
                module_manager.UnloadAllDynamicAssets();
            }

            if (valid_scene_count == 2)
            {
                module_manager.LoadAllDynamicAssets();
                StateController.RunOrDefer(GameState.PlayerReady, new GameStateEventHandler(OnPlayerReady), GameStatePriority.Medium);
                StateController.RunOrDefer(GameState.PlayerReady, new GameStateEventHandler(M113Tow.Convert), GameStatePriority.Medium);

                valid_scene_count = 0;
            }
        }
    }
}
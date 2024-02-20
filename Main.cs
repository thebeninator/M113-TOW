using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHPC.AI;
using GHPC.Camera;
using GHPC.Player;
using GHPC.State;
using GHPC.Vehicle;
using M113Tow;
using MelonLoader;
using GHPC.Utility;
using UnityEngine;
using GHPC.Weapons;
using GHPC.Equipment.Optics;
using GHPC;
using GHPC.Crew;

[assembly: MelonInfo(typeof(M113TowMod), "M113 TOW", "1.0.0", "ATLAS")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace M113Tow
{
    public class M113TowMod : MelonMod
    {
        GameObject m220;
        GameObject elevation_armor;
        GameObject[] vic_gos;
        MelonPreferences_Entry<int> random_chance;
        MelonPreferences_Entry<bool> thermals;

        public override void OnInitializeMelon()
        {
            MelonPreferences_Category cfg = MelonPreferences.CreateCategory("M113TOW");
            random_chance = cfg.CreateEntry<int>("Conversion Chance", 40);
            random_chance.Comment = "Integer (default: 40)";
            thermals = cfg.CreateEntry<bool>("Has Thermals", false);
            thermals.Comment = "the thermal sight blocks a ton of frontal vision in commander view lol";
        }

        public IEnumerator GetVics(GameState _)
        {
            vic_gos = GameObject.FindGameObjectsWithTag("Vehicle");

            yield break;
        }

        public IEnumerator Convert(GameState _)
        {
            foreach (GameObject vic_go in vic_gos)
            {
                Vehicle vic = vic_go.GetComponent<Vehicle>();

                if (vic == null) continue;
                if (vic.FriendlyName != "M113") continue;
                if (vic_go.GetComponent<Util.AlreadyConverted>() != null) continue;

                vic_go.AddComponent<Util.AlreadyConverted>();

                WeaponSystemInfo main_gun = vic.WeaponsManager.Weapons[0];

                int rand = UnityEngine.Random.Range(1, 100);

                if (rand > random_chance.Value) continue;

                Transform turret_ring = vic.transform.Find("M113A2_rig/HULL/Turret ring");
                turret_ring.GetChild(0).gameObject.SetActive(false);
                turret_ring.GetChild(2).gameObject.SetActive(false);

                GameObject tow = GameObject.Instantiate(m220, turret_ring);
                tow.SetActive(true);

                // 0.3672 -0.7227 0.711

                //tow.transform.localPosition = new Vector3(0.3672f, -0.6927f, 0.711f);
                tow.transform.localPosition = new Vector3(0.3272f, -0.7127f, 0.641f);

                tow.transform.Find("BGM71/tripod").gameObject.SetActive(false);

                LateFollow gunner = turret_ring.GetComponent<LateFollowTarget>()._lateFollowers[0];
                gunner._localPosShift = new Vector3(0f, -1.6824f, -0.01f);
                gunner.gameObject.transform.GetChild(1).GetComponent<CrewAnimTriggers>().SetAnimationTrigger("TOW_idle");

                Transform elev_scripts = tow.transform.Find("BGM71/AZIMUTH/ELEVATION/Elevation Scripts(Clone)");
                WeaponSystem tow_weapon = elev_scripts.Find("Launcher TOW").GetComponent<WeaponSystem>();

                main_gun.Role = WeaponSystemRole.MountedLauncher;
                main_gun.PreAimWeapon = WeaponSystemRole.MountedLauncher;
                main_gun.Name = "M220 TOW launcher";
                main_gun.Weapon = tow_weapon;

                main_gun.FCS = tow_weapon.FCS;
                main_gun.FCS.MainOptic = elev_scripts.Find("day sight/GPS").GetComponent<UsableOptic>();
                main_gun.FCS.MainOptic.gameObject.SetActive(true);

                if (thermals.Value)
                {
                    main_gun.FCS.NightOptic = elev_scripts.Find("day sight/FLIR").GetComponent<UsableOptic>();
                }
                else
                {
                    Util.GetDayOptic(main_gun.FCS).slot.LinkedNightSight = null;
                    tow.transform.Find("BGM71/AZIMUTH/ELEVATION/night sight").gameObject.SetActive(false);
                }

                main_gun.FCS.Awake();
                main_gun.FCS.Mounts[0] = vic.transform.Find("Turret ring scripts").GetComponent<AimablePlatform>();
                main_gun.FCS.Mounts[1].Transform = tow.transform.Find("BGM71/AZIMUTH/ELEVATION");
                main_gun.Weapon.Feed._totalReloadTime = 12f;

                vic.AimablePlatforms = main_gun.FCS.Mounts;

                GameObject elev_armor = GameObject.Instantiate(elevation_armor);
                LateFollow elev_armor_late_follow = elev_armor.GetComponent<LateFollow>();
                elev_armor_late_follow.FollowTarget = tow.transform.Find("BGM71/AZIMUTH/ELEVATION");
                elev_armor_late_follow._localPosShift = new Vector3(0f, -1.0525f, 0.175f);
                elev_armor_late_follow.transform.Find("ELEVATION").GetComponent<UniformArmor>().Unit = vic;
                elev_armor_late_follow.transform.Find("TOOOOOOOOOB").GetComponent<UniformArmor>().Unit = vic;

                vic.InfoBroker.AI.firingSpeedLimit = 1f;

                vic._friendlyName = "M113 TOW";
            }

            yield break;
        }

        public override void OnSceneWasLoaded(int idx, string scene_name)
        {
            if (scene_name == "MainMenu2_Scene" || scene_name == "LOADER_MENU" || scene_name == "LOADER_INITIAL" || scene_name == "t64_menu") return;

            foreach (Vehicle s in Resources.FindObjectsOfTypeAll(typeof(Vehicle)))
            {
                if (m220 != null) break;
                if (s.UniqueName != "STATIC_TOW") continue;

                m220 = GameObject.Instantiate(s.gameObject.transform.Find("BGM71_rig").gameObject);
                GameObject azimuth_scripts = GameObject.Instantiate(s.gameObject.transform.Find("Azimuth Scripts").gameObject);
                azimuth_scripts.GetComponent<Reparent>().NewParent = m220.transform.Find("BGM71/AZIMUTH");
                azimuth_scripts.GetComponent<Reparent>().Awake();

                GameObject elevation_scripts = GameObject.Instantiate(s.gameObject.transform.Find("Elevation Scripts").gameObject);
                elevation_scripts.GetComponent<Reparent>().NewParent = m220.transform.Find("BGM71/AZIMUTH/ELEVATION");
                elevation_scripts.GetComponent<Reparent>().Awake();

                elevation_armor = GameObject.Instantiate(s.gameObject.transform.Find("elevation armor").gameObject);

                m220.SetActive(false);

                break;
            }

            StateController.RunOrDefer(GameState.GameReady, new GameStateEventHandler(GetVics), GameStatePriority.Medium);
            StateController.RunOrDefer(GameState.GameReady, new GameStateEventHandler(Convert), GameStatePriority.Medium);
        }
    }
}

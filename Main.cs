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
using MelonLoader;
using GHPC.Utility;
using UnityEngine;
using GHPC.Weapons;
using GHPC.Equipment.Optics;
using GHPC;
using GHPC.Crew;
using GHPC.Effects;
using System.Data.SqlTypes;

[assembly: MelonInfo(typeof(M113TowMod), "M113 TOW", "1.0.3", "ATLAS")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace M113Extended
{
    public class M113ExtendedMod : MelonMod
    {
        static GameObject m220;
        static GameObject elevation_armor;
        static GameObject[] vic_gos;
        static AmmoClipCodexScriptable i_tow_clip_codex;
        MelonPreferences_Entry<int> random_chance;
        MelonPreferences_Entry<bool> thermals;
        MelonPreferences_Entry<bool> stab;
        MelonPreferences_Entry<bool> use_i_tow; 

        public override void OnInitializeMelon()
        {
            MelonPreferences_Category cfg = MelonPreferences.CreateCategory("M113TOW");
            random_chance = cfg.CreateEntry<int>("Conversion Chance", 40);
            random_chance.Comment = "Integer (default: 40)";
            thermals = cfg.CreateEntry<bool>("Has Thermals", false);
            thermals.Comment = "the thermal sight blocks a ton of frontal vision in commander view lol";
            stab = cfg.CreateEntry<bool>("Has Stabilizer", false);
            use_i_tow = cfg.CreateEntry<bool>("Use BGM-71C I-TOW", false);
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
                if (vic.UniqueName != "M113") continue;
                if (vic_go.GetComponent<Util.AlreadyConverted>() != null) continue;

                WeaponSystemInfo main_gun = vic.WeaponsManager.Weapons[0];

                int rand = UnityEngine.Random.Range(1, 100);

                if (rand > random_chance.Value) continue;

                Transform turret_ring = vic.transform.Find("M113A2_rig/HULL/Turret ring");
                turret_ring.GetChild(0).gameObject.SetActive(false);
                turret_ring.GetChild(2).gameObject.SetActive(false);
                turret_ring.GetChild(3).localPosition = new Vector3(-0.0055f, 0.7446f, 0.115f);

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
                main_gun.Weapon._impulseLocation = vic.transform;

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
                main_gun.FCS.Mounts[1].LocalEulerLimits.x = -10;
                main_gun.Weapon.Feed._totalReloadTime = 12f;

                GHPC.Weapons.AmmoRack rack = main_gun.Weapon.Feed.ReadyRack;
                if (use_i_tow.Value)
                    rack.ClipTypes = new AmmoType.AmmoClip[] { i_tow_clip_codex.ClipType };
                for (int i = 0; i <= 3; i++)
                    main_gun.Weapon.Feed.ReadyRack.AddInvisibleClip(main_gun.Weapon.Feed.ReadyRack.ClipTypes[0]);

                if (use_i_tow.Value)
                {
                    List<AmmoType.AmmoClip> new_stored_clips = new List<AmmoType.AmmoClip>();
                    for (int i = 0; i < rack.StoredClips.Count; i++)
                    {
                        new_stored_clips.Add(i_tow_clip_codex.ClipType);
                    }

                    rack.StoredClips = new_stored_clips.ToList();
                    main_gun.Weapon.Feed.AmmoTypeInBreech = null;
                    main_gun.Weapon.Feed.LoadedClipType = null;
                }

                if (stab.Value)
                {
                    main_gun.FCS.CurrentStabMode = StabilizationMode.Vector;
                    main_gun.FCS.StabsActive = true;
                    for (int i = 0; i <= 1; i++)
                    {
                        main_gun.FCS.Mounts[i].Stabilized = true;
                        main_gun.FCS.Mounts[i]._stabActive = true;
                        main_gun.FCS.Mounts[i]._stabMode = StabilizationMode.Vector;
                    }
                }

                vic.AimablePlatforms = main_gun.FCS.Mounts;

                GameObject elev_armor = GameObject.Instantiate(elevation_armor);
                LateFollow elev_armor_late_follow = elev_armor.GetComponent<LateFollow>();
                elev_armor_late_follow.FollowTarget = tow.transform.Find("BGM71/AZIMUTH/ELEVATION");
                elev_armor_late_follow._localPosShift = new Vector3(0f, -1.0525f, 0.175f);
                elev_armor_late_follow.transform.Find("ELEVATION").GetComponent<UniformArmor>().Unit = vic;
                elev_armor_late_follow.transform.Find("TOOOOOOOOOB").GetComponent<UniformArmor>().Unit = vic;

                vic.InfoBroker.AI.firingSpeedLimit = 1f;

                LateFollowTarget hull_late_follower = vic.GetComponent<LateFollowTarget>();
                LateFollow hull_armor_follow = hull_late_follower._lateFollowers[0].name == "HULL ARMOR" ? hull_late_follower._lateFollowers[0] : hull_late_follower._lateFollowers[1];
                GameObject ammo_box = hull_armor_follow.transform.Find("HULL AAR").Find("spare ammo").gameObject;
                ammo_box.GetComponent<UniformArmor>()._name = "spare TOW missiles";
                ammo_box.transform.localScale = new Vector3(0.2227f, 0.5722f, 1.0287f);
                ammo_box.GetComponent<FlammableItem>()._explosive = true;
                ammo_box.GetComponent<FlammableItem>()._tntEquivalent = 5f;

                FlammablesCluster flam = ammo_box.GetComponent<FlammableItem>()._cluster;
                flam.ContainsExplosives = true;

                GameObject second_ammo_box = GameObject.Instantiate(ammo_box, hull_armor_follow.transform.Find("HULL AAR"));
                second_ammo_box.transform.localPosition = new Vector3(1.01f, 1.117f, 0.297f);
                second_ammo_box.GetComponent<FlammableItem>().RegisterCluster(flam);

                GameObject third_ammo_box = GameObject.Instantiate(ammo_box, hull_armor_follow.transform.Find("HULL AAR"));
                third_ammo_box.transform.localPosition = new Vector3(-0.0299f, 0.6075f, -0.7725f);
                third_ammo_box.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                third_ammo_box.GetComponent<FlammableItem>().RegisterCluster(flam);

                GameObject fourth_ammo_box = GameObject.Instantiate(ammo_box, hull_armor_follow.transform.Find("HULL AAR"));
                fourth_ammo_box.transform.localPosition = new Vector3(-0.0299f, 0.8275f, -0.7725f);
                fourth_ammo_box.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                fourth_ammo_box.GetComponent<FlammableItem>().RegisterCluster(flam);

                flam.Items.Add(second_ammo_box.GetComponent<FlammableItem>());
                flam.Items.Add(third_ammo_box.GetComponent<FlammableItem>());
                flam.Items.Add(fourth_ammo_box.GetComponent<FlammableItem>());

                vic._friendlyName = "M113A2 TOW";
                vic_go.AddComponent<Util.AlreadyConverted>();
            }

            yield break;
        }

        public override void OnSceneWasLoaded(int idx, string scene_name)
        {
            if (Util.menu_screens.Contains(scene_name)) return;

            foreach (Vehicle s in Resources.FindObjectsOfTypeAll(typeof(Vehicle)))
            {
                if (m220 != null) break;
                if (s.gameObject.name != "TOW") continue;

                foreach (AmmoClipCodexScriptable a in Resources.FindObjectsOfTypeAll(typeof(AmmoClipCodexScriptable)))
                {
                    if (a.name == "clip_I-TOW") i_tow_clip_codex = a;
                }

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

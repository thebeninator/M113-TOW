using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
using GHPC.Camera;
using GHPC.Weaponry;
using ModUtil;
using GHPC.Player;
using VanillaAssetUtil.AssetUtil;

namespace M113Tow
{
    public class M113Tow : Module
    {
        private static GameObject m220;
        private static GameObject elevation_armor;
        private static AmmoClipCodexScriptable itow_clip_codex = new AmmoClipCodexScriptable();
        private static MelonPreferences_Entry<int> random_chance;
        private static MelonPreferences_Entry<bool> thermals;
        private static MelonPreferences_Entry<bool> stab;
        private static MelonPreferences_Entry<bool> use_itow;

        public static void Config(MelonPreferences_Category cfg)
        {
            random_chance = cfg.CreateEntry<int>("Conversion Chance", 40);
            random_chance.Comment = "Integer (default: 40)";
            thermals = cfg.CreateEntry<bool>("Has Thermals", false);
            thermals.Comment = "the thermal sight blocks a ton of frontal vision in commander view lol";
            stab = cfg.CreateEntry<bool>("Has Stabilizer", false);
            use_itow = cfg.CreateEntry<bool>("Use BGM-71C I-TOW", false);
        }

        private static void HandleConversion(Vehicle vic)
        {
            if (vic == null) return;
            if (vic.UniqueName != "M113") return;

            WeaponsManager weapons_manager = vic.WeaponsManager;
            WeaponSystemInfo main_gun = weapons_manager.Weapons[0];

            int rand = UnityEngine.Random.Range(1, 100);

            if (rand > random_chance.Value) return;

            Transform turret_ring = vic.transform.Find("M113A2_rig/HULL/Turret ring");
            turret_ring.GetChild(0).gameObject.SetActive(false);
            turret_ring.GetChild(2).gameObject.SetActive(false);
            turret_ring.GetChild(3).localPosition = new Vector3(-0.0055f, 0.7446f, 0.115f);

            GameObject tow = GameObject.Instantiate(m220, turret_ring);
            tow.SetActive(true);
            tow.transform.Find("BGM71/AZIMUTH/Azimuth Scripts").gameObject.SetActive(true);
            tow.transform.Find("BGM71/AZIMUTH/ELEVATION/Elevation Scripts").gameObject.SetActive(true);
            tow.transform.Find("BGM71/tripod").gameObject.SetActive(false);

            // 0.3672 -0.7227 0.711
            //tow.transform.localPosition = new Vector3(0.3672f, -0.6927f, 0.711f);
            tow.transform.localPosition = new Vector3(0.3272f, -0.7127f, 0.641f);

            LateFollow gunner = turret_ring.GetComponent<LateFollowTarget>()._lateFollowers[0];
            gunner._localPosShift = new Vector3(0f, -1.6824f, -0.01f);
            gunner.gameObject.transform.GetChild(1).GetComponent<CrewAnimTriggers>().SetAnimationTrigger("TOW_idle");

            Transform elev_scripts = tow.transform.Find("BGM71/AZIMUTH/ELEVATION/Elevation Scripts");
            WeaponSystem tow_weapon = elev_scripts.Find("Launcher TOW").GetComponent<WeaponSystem>();

            AmmoFeed tow_feed = tow_weapon.Feed;
            AmmoFeed.ReloadStage original_stage = tow_feed.ClipReloadStages[0];
            tow_feed.ClipReloadStages = new AmmoFeed.ReloadStage[]
            {
                new AmmoFeed.ReloadStage()
                {
                    Duration = 5f,
                    StageClips = new AudioClip[] { },
                    AnimatedParts = new AnimatedPart[] { },
                    ClipReloadFMODParameters = new AmmoFeed.ReloadStage.FMODParameter[] {}
                },
                original_stage,
            };

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
            if (use_itow.Value)
            {
                rack.ClipTypes = new AmmoType.AmmoClip[] { itow_clip_codex.ClipType };
            }
            for (int i = 0; i <= 3; i++)
            {
                main_gun.Weapon.Feed.ReadyRack.AddInvisibleClip(main_gun.Weapon.Feed.ReadyRack.ClipTypes[0]);
            }

            if (use_itow.Value)
            {
                List<AmmoType.AmmoClip> new_stored_clips = new List<AmmoType.AmmoClip>();
                for (int i = 0; i < rack.StoredClips.Count; i++)
                {
                    new_stored_clips.Add(itow_clip_codex.ClipType);
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

            //GameObject elev_armor = GameObject.Instantiate(elevation_armor);
            //LateFollow elev_armor_late_follow = elev_armor.GetComponent<LateFollow>();
            //elev_armor_late_follow.FollowTarget = tow.transform.Find("BGM71/AZIMUTH/ELEVATION");
            //elev_armor_late_follow._localPosShift = new Vector3(0f, -1.0525f, 0.175f);
            //elev_armor_late_follow.transform.Find("ELEVATION").GetComponent<UniformArmor>().Unit = vic;
            //elev_armor_late_follow.transform.Find("TOOOOOOOOOB").GetComponent<UniformArmor>().Unit = vic;

            LateFollowTarget hull_late_follower = vic.GetComponent<LateFollowTarget>();
            LateFollow hull_armor_follow = hull_late_follower._lateFollowers[0].name == "HULL ARMOR" ? hull_late_follower._lateFollowers[0] : hull_late_follower._lateFollowers[1];
            GameObject ammo_box = hull_armor_follow.transform.Find("HULL AAR").Find("spare ammo").gameObject;
            ammo_box.GetComponent<UniformArmor>()._name = "spare TOW missiles";
            ammo_box.GetComponent<FlammableItem>()._explosive = true;
            ammo_box.GetComponent<FlammableItem>()._tntEquivalent = 5f;
            ammo_box.transform.localScale = new Vector3(0.2227f, 0.5722f, 1.0287f);

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

            List<CameraSlot> cam_slots = new List<CameraSlot> 
            {
                vic.transform.Find("M113A2_rig/HULL/Turret ring/commander head").GetComponent<CameraSlot>(),
                main_gun.FCS.MainOptic.slot,
            };

            if (thermals.Value)
            {
                cam_slots.Add(main_gun.FCS.MainOptic.slot.LinkedNightSight);
            }

            vic._designatedCameraSlots = cam_slots.ToArray();

            if (vic.GetInstanceID() == PlayerInput.Instance.CurrentPlayerUnit.GetInstanceID())
            {
                CameraManager.Instance.RescanCamSlots(vic.DesignatedCameraSlots);
            }

            vic.InfoBroker.AI.firingSpeedLimit = 0f;
            vic.TargetSpotterSettings._bestDayVision = GHPC.AI.TargetSpotterSettings.DayVisionQuality.Telescope;
            vic.TargetSpotterSettings._bestNightVision = GHPC.AI.TargetSpotterSettings.NightVisionQuality.Gen1Thermal;
            vic.TargetSpotterSettings._sightDistance = 3000f;
        }

        public static IEnumerator Convert(GameState _)
        {
            foreach (Vehicle vic in Mod.vics)
            {
                HandleConversion(vic);
            }

            yield break;
        }

        public override void LoadDynamicAssets()
        {
            AssetUtilApi.LoadVanillaVehicle("M2BRADLEY", temp: true);
            m220 = AssetUtilApi.CloneVanillaGameObject("STATIC_TOW", "BGM71_rig");

            itow_clip_codex.name = "m113 itow";
            itow_clip_codex.ClipType = new AmmoType.AmmoClip()
            {
                Capacity = 1,
                MinimalPattern = new AmmoCodexScriptable[] { Resources.FindObjectsOfTypeAll<AmmoCodexScriptable>().Where(o => o.name == "ammo_I-TOW").First() },
                Name = "BGM-71C I-TOW"
            };

            GameObject azimuth_scripts = AssetUtilApi.CloneVanillaGameObject("STATIC_TOW", "Azimuth Scripts");
            azimuth_scripts.GetComponent<Reparent>().NewParent = m220.transform.Find("BGM71/AZIMUTH");
            azimuth_scripts.GetComponent<Reparent>().Awake();
            AimablePlatform azimuth_platform = azimuth_scripts.GetComponent<AimablePlatform>();
            azimuth_platform.Transform = m220.transform.Find("BGM71/AZIMUTH");

            GameObject elevation_scripts = AssetUtilApi.CloneVanillaGameObject("STATIC_TOW", "Elevation Scripts");
            elevation_scripts.GetComponent<Reparent>().NewParent = m220.transform.Find("BGM71/AZIMUTH/ELEVATION");
            elevation_scripts.GetComponent<Reparent>().Awake();
            AimablePlatform elevation_platform = azimuth_scripts.GetComponent<AimablePlatform>();
            elevation_platform.Transform = m220.transform.Find("BGM71/AZIMUTH/ELEVATION");

            elevation_armor = AssetUtilApi.CloneVanillaGameObject("STATIC_TOW", "elevation armor");
            elevation_armor.GetComponent<LateFollow>().FollowTarget = m220.transform.Find("BGM71/AZIMUTH/ELEVATION");
        }
    }
}

using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHPC;
using GHPC.Vehicle; 

namespace M113Extended
{
    public class TOW
    {
        static MelonPreferences_Entry<int> random_chance;
        static MelonPreferences_Entry<bool> thermals;
        static MelonPreferences_Entry<bool> stab;

        public static void Config(MelonPreferences_Category cfg) {
            random_chance = cfg.CreateEntry<int>("Conversion Chance", 40);
            random_chance.Comment = "Integer (default: 40)";
            thermals = cfg.CreateEntry<bool>("Has Thermals", false);
            thermals.Comment = "the thermal sight blocks a ton of frontal vision in commander view lol";
            stab = cfg.CreateEntry<bool>("Has Stabilizer", false);
        }

        public static void Convert(Vehicle vic) { 
        
        }
    }
}

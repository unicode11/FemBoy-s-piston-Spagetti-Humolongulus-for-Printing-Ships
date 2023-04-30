using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Utils;
using Sandbox.Game.Gui;
using System.Runtime.CompilerServices;
using VRage.Library.Utils;
using Sandbox.ModAPI.Weapons;
using Sandbox.Engine.Networking;

namespace Xep
{
    public sealed class Program : MyGridProgram
    {
        // У сварщиков каждые 60 тиков проверяем условие "Работает(сваривает)"
        //      Нет -> двигаем пистон
        //      Да -> останавливаем(или не двигаем) пистон
        // Как только первый пистон достигнет лимита -> активируется следующий
        // Как только у проектора закончатса блоки на сварку -> он отключается и вот тебе ебать корабль
        // чтобы определять который пистон уже закончил работу - можно в их кустом дату писать их номер и статус
        // типа "1(номер)100(окончания)"(закончил) "2200(не закончил)"


        //
        //
        // ===================== NO COPY ABOVE THIS POINT =========================

        IMyProjector projector;
        IMyShipWelder welder;
        IMyPistonBase piston;

        List<IMyTerminalBlock> piston_list;
        List<IMyTerminalBlock> welder_list;
        List<IMyTerminalBlock> projector_list;



        public Program ()
        {
            piston_list = new List<IMyTerminalBlock>();
            welder_list = new List<IMyTerminalBlock>();
            projector_list = new List<IMyTerminalBlock>();
        }

        public void Main(string argument)
        {
            IMyBlockGroup LIST = GridTerminalSystem.GetBlockGroupWithName("DOCKYARD");
            LIST.GetBlocksOfType<IMyPistonBase>(piston_list);
            LIST.GetBlocksOfType<IMyShipWelder>(welder_list);
            LIST.GetBlocksOfType<IMyProjector>(projector_list);

            var NUM = 1;
            var NUM2 = 1;

            foreach (IMyTerminalBlock PROJECTOR in projector_list)
            { PROJECTOR.CustomName = ("[DY] PROJECTOR"); 
              projector = (IMyProjector)PROJECTOR;
            }

            foreach (IMyTerminalBlock WELDER in welder_list)
            { WELDER.CustomName = ($"[DY] WELDER {NUM2}");
              WELDER.CustomData = ($"{NUM2}100"); NUM2++;
              welder = (IMyShipWelder)WELDER;
            }

            foreach (IMyTerminalBlock PISTON in piston_list)
            { PISTON.CustomName = ($"[DY] PISTON {NUM}");
              PISTON.CustomData = ($"{NUM}100"); NUM++;
              piston = (IMyPistonBase)PISTON;
            }

            Echo("All preps are done.\n" +
                "Now type \"+RUN_DEEZ_SHIT\" for script to start.\n" +
                "Double check that your projection is alligned correctly.");


            // MECHANISM SHIT. DO NOT TOUCH ANYTHING, I DON'T KNOW HOW IT WORKS EITHER.
            if (argument == "+RUN_DEEZ_SHIT")
            {
                Echo("SCRIPT STARTED");
                if (projector.Enabled)
                {
                    Echo("у меня муха во рту");
                }

            }

        }
    }
}
        // ================================================


// SCRIPT START ABOVE
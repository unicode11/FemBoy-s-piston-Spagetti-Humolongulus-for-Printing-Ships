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
using Sandbox.ModAPI;
using Sandbox.Game.Gui;
using System.Runtime.CompilerServices;
using VRage.Library.Utils;

namespace Script1
{
    public sealed class Program : MyGridProgram
    {
        // У сварщиков каждые 60 тиков проверяем условие "Работает(сваривает)"
        //      Нет -> двигаем пистон
        //      Да -> останавливаем(или не двигаем) пистон
        // Как только первый пистон достигнет лимита -> активируется следующий
        // Как только у проектора закончатса блоки на сварку -> он отключается и вот тебе ебать корабль


        //
        //
        // SCRIPT START BELOVE

        //;
        // ===================== NO COPY BEYOND THIS POINT =========================
        public void Main(string argument)
        {
            var NUM = 1;
            var NUM2 = 1;
            Sandbox.ModAPI.Ingame.IMyBlockGroup LIST = GridTerminalSystem.GetBlockGroupWithName("DOCKYARD");



            List<Sandbox.ModAPI.Ingame.IMyPistonBase> PISTON = new List<Sandbox.ModAPI.Ingame.IMyPistonBase>();
            LIST.GetBlocksOfType(PISTON);
            List<Sandbox.ModAPI.Ingame.IMyShipWelder> WELDER = new List<Sandbox.ModAPI.Ingame.IMyShipWelder>();
            LIST.GetBlocksOfType(WELDER);

            foreach (Sandbox.ModAPI.Ingame.IMyShipWelder BLYAT in WELDER)
            {
                BLYAT.CustomName = ($"[DY] WELDER {NUM2}");
                NUM2++;
            }

            foreach (Sandbox.ModAPI.Ingame.IMyPistonBase BLYAT in PISTON)
            {
                BLYAT.CustomName = ($"[DY] PISTON {NUM}");
                NUM++;

                if (argument == "OwO")
                {
                    BLYAT.Reverse();
                }

            }

        }
        // ================================================
    }
}


// SCRIPT START ABOVE
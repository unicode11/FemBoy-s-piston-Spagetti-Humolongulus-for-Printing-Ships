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
using System.Security.Cryptography.X509Certificates;

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
        // далее проверка кустом даты у каждого письтона 
        // цикл можно сделать при помощи таймера


        //
        //
        // ===================== NO COPY ABOVE THIS POINT =========================
        float Step = 1f;
        float MinAngle = 5f;
        string _RotorAKey = "_AA1";
        string _RotorBKey = "_AA2";
        string _RotorCKey = "_AA3";
        string _PanelKey = "_AAP";
        string _ScRotorReverce = "_AARv";
        string _ScMerges = "_AAMB";
        string _ScProjectors = "_AAPj";
        string _WeldersGROUP = "gWelders";

        bool AutoAttachWheels = true; //When isProjecting only

        int TimeToReverse = 10;//For _AARv rotors

        struct tCore
        {
            public int State;
            public List<IMyTextPanel> InfoPanels;
            public bool IsInfoPanelsPresent;
            public IMyMotorAdvancedStator Motor1;
            public IMyMotorAdvancedStator Motor2;
            public IMyMotorAdvancedStator Motor3;
            public List<IMyMotorStator> ScRotorReverse;
            public List<IMyShipMergeBlock> ScMerges;
            public List<IMyProjector> Projectors;
            public List<IMyProjector> ScProjectors;
            public List<IMyShipWelder> ScWelders;
            public float MZ1;
            public float MZ2;
            public float MZ3;
            public float Value;
            public List<double> ScA;
            public List<int> ScT;
        }

        tCore Core = new tCore();
        const string _On = "OnOff_On"; const string _Off = "OnOff_Off";

        public Program() { Runtime.UpdateFrequency = UpdateFrequency.Update10; }

        public void Main(string Argument, UpdateType UpdateSource)
        {
            if (Core.State < 1) { Boot(); return; }

            if (!string.IsNullOrEmpty(Argument)) { ProceedArgument(Argument); }
            else
            {
                if (Core.State == 1) { Thread(); }
                if (Core.State == 2) { Wizard(); }
            }
        }

        void Boot()
        {
            Core = new tCore() { Value = MinAngle, ScA = new List<double>(), ScT = new List<int>() };
            bool Wiz = Base();
            InitDevices();
            Core.IsInfoPanelsPresent = Core.InfoPanels.Count > 0;
            AutoAttachWheels &= Core.Projectors.Count > 0;
            if (Wiz) { Core.State = 2; }
            else if (Core.Motor1 != null && Core.Motor2 != null && Core.Motor3 != null)
            {
                Core.State = 1;
                Echo("Started successfully\n\nProjectors: " + Core.Projectors.Count + "\nWheels auto attach: " + AutoAttachWheels
                    + "\nSc:Reverse rotors: " + Core.ScRotorReverse.Count + "\nSc:Merge blocks: " + Core.ScMerges.Count
                    + "\nSc:Projectors: " + Core.ScProjectors.Count + "\nSc:Welders: " + Core.ScWelders.Count);
            }
        }

        bool Base(bool Write = false)
        {
            if (!Write)
            {
                string S = Me.CustomData;
                if (!String.IsNullOrEmpty(S))
                {
                    string[] C = S.Split(new string[] { ";" }, StringSplitOptions.None);
                    if (C.Length == 4)
                    {
                        Core.MZ1 = (float)Convert.ToDouble("0" + C[0].Trim());
                        Core.MZ2 = (float)Convert.ToDouble("0" + C[1].Trim());
                        Core.MZ3 = (float)Convert.ToDouble("0" + C[2].Trim());
                        Core.Value = (float)Convert.ToDouble("0" + C[3].Trim());
                        if (Core.Value < MinAngle) { Core.Value = MinAngle; }
                        if (Core.Value >= 90 - MinAngle) { Core.Value = 90 - MinAngle; }
                    }
                    else { return true; }
                }
                else { return true; }
            }
            else { Me.CustomData = Core.MZ1.ToString() + ";" + Core.MZ2.ToString() + ";" + Core.MZ3.ToString() + ";" + Core.Value.ToString(); }
            return false;
        }

        void InitDevices()
        {
            Me.CustomName = " •• Printer";
            Core.InfoPanels = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(Core.InfoPanels, R => R.CustomName.ToLower().Contains(_PanelKey.ToLower()));
            Core.Projectors = new List<IMyProjector>();
            GridTerminalSystem.GetBlocksOfType(Core.Projectors);
            List<IMyMotorAdvancedStator> M = new List<IMyMotorAdvancedStator>();
            GridTerminalSystem.GetBlocksOfType(M, R => R.CustomName.ToLower().Contains(_RotorAKey.ToLower()));
            if (M.Count == 1) { Core.Motor1 = M[0]; } else { Echo("Check for : Adv.Motor " + _RotorAKey); }
            GridTerminalSystem.GetBlocksOfType(M, R => R.CustomName.ToLower().Contains(_RotorBKey.ToLower()));
            if (M.Count == 1) { Core.Motor2 = M[0]; } else { Echo("Check for : Adv.Motor " + _RotorBKey); }
            GridTerminalSystem.GetBlocksOfType(M, R => R.CustomName.ToLower().Contains(_RotorCKey.ToLower()));
            if (M.Count == 1) { Core.Motor3 = M[0]; } else { Echo("Check for : Adv.Motor " + _RotorCKey); }
            Core.ScRotorReverse = new List<IMyMotorStator>();
            GridTerminalSystem.GetBlocksOfType(Core.ScRotorReverse, R => R.CustomName.ToLower().Contains(_ScRotorReverce.ToLower()));
            Core.ScMerges = new List<IMyShipMergeBlock>();
            GridTerminalSystem.GetBlocksOfType(Core.ScMerges, R => R.CustomName.ToLower().Contains(_ScMerges.ToLower()));
            Core.ScProjectors = new List<IMyProjector>();
            GridTerminalSystem.GetBlocksOfType(Core.ScProjectors, R => R.CustomName.ToLower().Contains(_ScProjectors.ToLower()));
            Core.ScWelders = new List<IMyShipWelder>();
            IMyBlockGroup G = GridTerminalSystem.GetBlockGroupWithName(_WeldersGROUP);
            if (G != null) { G.GetBlocksOfType(Core.ScWelders); }
        }

        void Wizard() { Echo("   •• Setup •• \nRotate the rotors in their working areas and Run> R"); }

        int Timer = 0;
        void Thread()
        {
            Timer = Timer > 5 ? 0 : Timer + 1;
            if (Timer == 0 && AutoAttachWheels) { CheckUpWheels(); }
            if (Timer == 0 && Core.ScT.Count > 0)
            {
                if (Core.ScT[0] == -700 || Core.ScT[0] == -800) { Core.ScT.RemoveAt(0); Core.ScA.RemoveAt(0); }
                else if (Core.ScT[0] < -700 && Core.ScT[0] > -800) { ScRotorReverse(true); Core.ScT[0]++; }
                else if (Core.ScT[0] < -800 && Core.ScT[0] > -900) { ScRotorReverse(false); Core.ScT[0]++; }
                else if (Core.ScT[0] == -900 || Core.ScT[0] == -901) { ScMergesTurn(Core.ScT[0] == -900); Core.ScT.RemoveAt(0); Core.ScA.RemoveAt(0); }
                else if (Core.ScT[0] == -902 || Core.ScT[0] == -903) { ScProjectorsTurn(Core.ScT[0] == -902); Core.ScT.RemoveAt(0); Core.ScA.RemoveAt(0); }
                else if (Core.ScT[0] == -904 || Core.ScT[0] == -905) { ScWeldersTurn(Core.ScT[0] == -904); Core.ScT.RemoveAt(0); Core.ScA.RemoveAt(0); }
                else if (Core.Value == Core.ScA[0] && Core.Motor1.RotorLock) { Core.ScT[0]--; if (Core.ScT[0] < 1) { Core.ScT.RemoveAt(0); Core.ScA.RemoveAt(0); } }
                if (Core.ScT.Count > 0 && (float)Core.ScA[0] > 0) { Core.Value = (float)Core.ScA[0]; }
            }
            UpdateAngles();
            UpdateInfoScreen();
        }

        void ProceedArgument(string Argument)
        {
            string A = Argument.ToLower();
            if (A == "r") { InitZeroValues(); Core.State = 0; }
            if (A == "+") { Core.Value += Step; }
            if (A == "-") { Core.Value -= Step; }
            if (A == "0") { Core.Value = MinAngle; }
            if (A.StartsWith("a=")) { Core.Value = (float)Recognize(Argument); }
            if (Core.Value < MinAngle) { Core.Value = MinAngle; }
            if (Core.Value >= 90 - MinAngle) { Core.Value = 90 - MinAngle; }
            if (A.StartsWith("s:")) { RecognizeScenario(Argument); }
            Base(true);
        }

        void InitZeroValues()
        {
            Core.MZ1 = (int)(Core.Motor1.Angle / AR / 90 + 1) * 90;
            Core.MZ2 = (int)(Core.Motor2.Angle / AR / 90) * 90;
            Core.MZ3 = (int)(Core.Motor3.Angle / AR / 90) * 90;
            Base(true);
            Echo("Setup Complete");
        }

        void UpdateAngles()
        {
            bool R;
            R = SetMotorAngle(Core.MZ1 - Core.Value, ref Core.Motor1); if (Core.Motor1.RotorLock != R) { Core.Motor1.RotorLock = R; }
            R = SetMotorAngle(Core.MZ2 + Core.Value * 2, ref Core.Motor2, 2f); if (Core.Motor2.RotorLock != R) { Core.Motor2.RotorLock = R; }
            R = SetMotorAngle(Core.MZ3 + Core.Value, ref Core.Motor3, 1f); if (Core.Motor3.RotorLock != R) { Core.Motor3.RotorLock = R; }
        }

        double Recognize(string Argument, double Default = 0, string Separator = "=")
        {
            string[] C = Argument.Split(new string[] { Separator }, StringSplitOptions.None);
            return C.Length > 1 ? Convert.ToDouble(C[1]) : Default;
        }

        void RecognizeScenario(string Argument)
        {
            Argument = Argument.Substring(2);
            double V;
            Core.ScA = new List<double>(); Core.ScT = new List<int>();
            string[] C = Argument.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string T in C)
            {
                if (T.ToLower() == "r+") { Core.ScA.Add(MinAngle); Core.ScT.Add(-700 - TimeToReverse); }
                else if (T.ToLower() == "r-") { Core.ScA.Add(MinAngle); Core.ScT.Add(-800 - TimeToReverse); }
                else if (T.ToLower() == "m+") { Core.ScA.Add(-1); Core.ScT.Add(-900); }
                else if (T.ToLower() == "m-") { Core.ScA.Add(-1); Core.ScT.Add(-901); }
                else if (T.ToLower() == "p+") { Core.ScA.Add(-1); Core.ScT.Add(-902); }
                else if (T.ToLower() == "p-") { Core.ScA.Add(-1); Core.ScT.Add(-903); }
                else if (T.ToLower() == "w+") { Core.ScA.Add(-1); Core.ScT.Add(-904); }
                else if (T.ToLower() == "w-") { Core.ScA.Add(-1); Core.ScT.Add(-905); }
                else
                {
                    string[] P = T.Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                    if (P.Length == 2) { Double.TryParse(P[0], out V); Core.ScA.Add(V); Double.TryParse(P[1], out V); Core.ScT.Add((int)V); }
                }
            }
            Echo("Script: " + Core.ScA.Count + " steps");
        }

        void ScRotorReverse(bool Pos)
        { foreach (IMyMotorStator M in Core.ScRotorReverse) { M.TargetVelocityRPM = Pos ? Math.Abs(M.TargetVelocityRPM) : -Math.Abs(M.TargetVelocityRPM); } }

        void ScMergesTurn(bool On)
        { foreach (IMyShipMergeBlock M in Core.ScMerges) { M.ApplyAction(On ? _On : _Off); } }

        void ScWeldersTurn(bool On)
        { foreach (IMyShipWelder M in Core.ScWelders) { M.ApplyAction(On ? _On : _Off); } }

        void ScProjectorsTurn(bool On)
        { foreach (IMyProjector M in Core.ScProjectors) { M.ApplyAction(On ? _On : _Off); } }

        string LastInfo = "";
        void UpdateInfoScreen()
        {
            string R = Core.Value.ToString();
            if (R != LastInfo) { LastInfo = R; foreach (IMyTextPanel P in Core.InfoPanels) { P.WriteText(R); } }
        }

        float AR = (float)Math.PI / 180;
        bool SetMotorAngle(float Angle, ref IMyMotorAdvancedStator Motor, float Mult = 1f)
        {
            float BrakeAngle = 3;
            float DX = Angle - Motor.Angle / AR;
            float Diff = 180 - (DX + 360) % 360;
            DX = Math.Abs(DX);
            bool R = DX < 1f;
            Motor.TargetVelocityRPM = (DX > BrakeAngle ? .5f * Mult : R ? 0 : .1f * Mult) * Math.Sign(Diff);
            return R;
        }

        void CheckUpWheels()
        {
            bool R = false; foreach (IMyProjector P in Core.Projectors) { R |= P.IsProjecting; }
            if (!R) { return; }
            List<IMyMotorSuspension> M = new List<IMyMotorSuspension>();
            GridTerminalSystem.GetBlocksOfType(M);
            foreach (IMyMotorSuspension W in M) { if (!W.IsAttached) { W.ApplyAction("Add Top Part"); } }
        }
    }
}
        // ================================================


// SCRIPT START ABOVE
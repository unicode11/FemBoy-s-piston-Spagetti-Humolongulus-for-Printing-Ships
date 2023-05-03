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


        //
        //
        // ===================== NO COPY ABOVE THIS POINT =========================
        string printerTag = "[Printer]"; // only consider blocks with this tag in the name as part of the printer. For example "Piston [Printer]" or "Welder 1 [Printer]"
        float printSpeedTotal = 0.5f; // piston extension speed | 0.5 seems the safe speed to print without missing any pieces afterwards
        string completionSound = "Objective complete"; // sound to be played on the soundblock(s) after print is completed. Case sensitive
        string incompleteSound = "Alert 2"; // sound to be played on the soundblock(s) when print is incomplete. Case sensitive
        Color lightColor = new Color(30, 30, 255); // color of lights that will be turned on during printing

        /* DO NOT EDIT BELOW THIS LINE */
        List<IMyPistonBase> pistons = new List<IMyPistonBase>();
        List<IMyShipWelder> welders = new List<IMyShipWelder>();
        List<IMyLightingBlock> lights = new List<IMyLightingBlock>();
        List<IMyProjector> projectors = new List<IMyProjector>();
        List<IMySoundBlock> soundblocks = new List<IMySoundBlock>();
        List<IMyButtonPanel> buttonPanels = new List<IMyButtonPanel>();
        PrinterState state;
        float speedPerPiston;
        int currentProjector = 1;
        double totalPrintTime;

        public enum PrinterState
        {
            Ready,
            Printing,
            Resetting,
            Incomplete
        }


        public Program()
        {
            // configure Printer
            ConfigurePrinter();

            // initiate Printer
            state = GetPrinterStateFromStorage();
        }

        private void ConfigurePrinter()
        {
            // set script to run automatically at slowest interval
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            ConfigureProgrammableBlockLCD();
            ConfigureWelders();
            ConfigureLights();
            ConfigureProjectors();
            ConfigureSoundBlocks();
            ConfigureButtonPanel();
            ConfigurePistons();
            CalculatePrintTime();

            Echo($"[Detected Blocks]\n" +
                $"{welders.Count} Welders\n" +
                $"{lights.Count} Lights\n" +
                $"{projectors.Count} Projectors\n" +
                $"{soundblocks.Count} Sound Blocks\n" +
                $"{buttonPanels.Count} Button Panels\n" +
                $"{pistons.Count} Pistons\n");
            Echo($"If you're missing blocks make sure to include '{printerTag}' in the name");

            // display ready
            SetLights(false);
            UpdateScreenWithPrintTime();
        }

        private PrinterState GetPrinterStateFromStorage()
        {
            if (String.IsNullOrWhiteSpace(Storage))
            {
                return PrinterState.Ready;
            }
            var parsedState = (PrinterState)Enum.Parse(typeof(PrinterState), Storage);
            if (Enum.IsDefined(typeof(PrinterState), parsedState))
            {
                return parsedState;
            }
            else
            {
                return PrinterState.Ready;
            }
        }

        private void ConfigureWelders()
        {
            GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(welders, block => block.CubeGrid.IsSameConstructAs(Me.CubeGrid) && IsTaggedForPrinter(block));
        }

        private void ConfigurePistons()
        {
            GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistons, block => block.CubeGrid.IsSameConstructAs(Me.CubeGrid) && IsTaggedForPrinter(block));

            // calculate speeds (based on piston count)
            speedPerPiston = (float)(printSpeedTotal / pistons.Count);
        }

        private void ConfigureLights()
        {
            GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, block => block.CubeGrid.IsSameConstructAs(Me.CubeGrid) && IsTaggedForPrinter(block));
            foreach (var light in lights)
            {
                light.Color = lightColor;
            }
        }

        private void ConfigureProjectors()
        {
            GridTerminalSystem.GetBlocksOfType<IMyProjector>(projectors, block => block.CubeGrid.IsSameConstructAs(Me.CubeGrid) && IsTaggedForPrinter(block));
        }

        private void ConfigureSoundBlocks()
        {
            GridTerminalSystem.GetBlocksOfType<IMySoundBlock>(soundblocks, block => block.CubeGrid.IsSameConstructAs(Me.CubeGrid) && IsTaggedForPrinter(block));
        }

        private void ConfigureButtonPanel()
        {
            // NOTE: Unfortunatly we can't set the actions for button (limitation in SE API). But we can change the button text
            GridTerminalSystem.GetBlocksOfType<IMyButtonPanel>(buttonPanels, block => block.CubeGrid.IsSameConstructAs(Me.CubeGrid) && IsTaggedForPrinter(block));
            foreach (var buttonPanel in buttonPanels)
            {

                // 3 button vertical panel
                if (buttonPanel.BlockDefinition.SubtypeId.Equals("VerticalButtonPanelLarge"))
                {
                    buttonPanel.SetCustomButtonName(0, "Start Printer");
                    buttonPanel.SetCustomButtonName(1, "Previous Blueprint");
                    buttonPanel.SetCustomButtonName(2, "Next Blueprint");
                    continue;  // go to next button panel
                }

                // 1 button button with screen
                if (buttonPanel.BlockDefinition.SubtypeId.Equals("LargeSciFiButtonTerminal"))
                {
                    buttonPanel.SetCustomButtonName(0, "Start Printer");
                    continue; // go to next button panel
                }

                // 4 panel button (ButtonPanelLarge or LargeSciFiButtonPanel)
                // button 1: previous blueprint
                // button 2: next blueprint
                // button 3: start (might not be, in that case button 4 will be start)
                buttonPanel.SetCustomButtonName(0, "Previous Blueprint");
                buttonPanel.SetCustomButtonName(1, "Next Blueprint");
                buttonPanel.SetCustomButtonName(2, "Start Printer");

                // if both button 3 and 4 are set; they are start and stop respectively
                // if only one of them is set, that is start and there is no stop button
                if (buttonPanel.IsButtonAssigned(2))
                {
                    buttonPanel.SetCustomButtonName(3, "Stop Printer");
                }
                else
                {
                    buttonPanel.SetCustomButtonName(3, "Start Printer");
                }

            }
        }

        private void ConfigureProgrammableBlockLCD()
        {
            IMyTextSurface mesurface0 = Me.GetSurface(0);
            mesurface0.ContentType = ContentType.TEXT_AND_IMAGE;
            mesurface0.FontSize = 2;
            mesurface0.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.CENTER;
            mesurface0.WriteText("Initializing");
        }

        private bool IsTaggedForPrinter(IMyTerminalBlock block)
        {
            return (block.CustomName.IndexOf(printerTag, StringComparison.OrdinalIgnoreCase) > -1);
        }

        private void CalculatePrintTime()
        {
            totalPrintTime = Math.Round(pistons[0].MaxLimit / speedPerPiston);
        }

        private string GetPrintTime()
        {
            TimeSpan printTime = TimeSpan.FromSeconds(totalPrintTime);
            return printTime.ToString(@"m\:ss");
        }
        private string GetTimeRemaining()
        {
            TimeSpan printTimeLeft = TimeSpan.FromSeconds(Math.Round(totalPrintTime - (pistons[0].CurrentPosition / pistons[0].MaxLimit * totalPrintTime)));
            return printTimeLeft.ToString(@"m\:ss");
        }

        private string GetBlocksProgress()
        {
            return $"{projectors[currentProjector - 1].TotalBlocks - projectors[currentProjector - 1].RemainingBlocks}/{projectors[currentProjector - 1].TotalBlocks}";
        }

        private void UpdateScreenStatusOnly()
        {
            Me.GetSurface(0).WriteText($"State: {state}");
        }

        private void UpdateScreenWithPrintTime()
        {
            Me.GetSurface(0).WriteText($"State: {state}\n\nPrint Time {GetPrintTime()}");
        }

        private void UpdateScreenWithRemainingPrintTime()
        {
            if (projectors.Count > 0)
            {
                Me.GetSurface(0).WriteText(
                    $"State: {state}\n" +
                    $"Time Remaining {GetTimeRemaining()}\n" +
                    $"Progress {GetBlocksProgress()}"
                );
            }
            else
            {
                Me.GetSurface(0).WriteText(
                    $"State: {state}\n" +
                    $"Time Remaining {GetTimeRemaining()}"
                );
            }
        }

        private void UpdateScreenWithIncompleteMessage()
        {
            if (projectors.Count > 0)
            {
                Me.GetSurface(0).WriteText(
                    $"State: {state}\n" +
                    $"Progress {GetBlocksProgress()}\n\n" +
                    "Start to Restart\n" +
                    "Stop to Abort"
                );
            }
            else
            {
                Me.GetSurface(0).WriteText(
                    $"State: {state}\n\n" +
                    "Start to Restart\n" +
                    "Stop to Abort"
                );
            }
        }


        private void SetWelders(bool value)
        {
            foreach (IMyShipWelder welder in welders)
            {
                welder.Enabled = value;
            }
        }

        private void SetLights(bool value)
        {
            foreach (IMyLightingBlock light in lights)
            {
                light.Enabled = value;
            }
        }

        private void PlaySound(string sound)
        {
            foreach (IMySoundBlock soundblock in soundblocks)
            {
                soundblock.SelectedSound = sound;
                soundblock.Play();
            }
        }

        private void ExtendPistons()
        {
            foreach (IMyPistonBase piston in pistons)
            {
                piston.Velocity = speedPerPiston;
                piston.Enabled = true;
            }
        }

        private void RetractPistonsFast()
        {
            foreach (IMyPistonBase piston in pistons)
            {
                piston.Velocity = -10f;
                piston.Enabled = true;
            }
        }

        private bool IsPistonAtMaximumPosition()
        {
            // only need to check one piston, as they all move at same speed and have same limit
            return pistons[0].CurrentPosition == pistons[0].MaxLimit;
        }

        private bool IsPistonAtMinimumPosition()
        {
            // only need to check one piston, as they all move at same speed and have same limit
            return !(pistons[0].CurrentPosition > pistons[0].MinLimit);
        }

        private bool IsBluePrintCompleted()
        {
            if (projectors.Count > 0)
            {
                return projectors[currentProjector - 1].RemainingBlocks == 0;
            }
            return true;
        }

        private void SwitchProjector()
        {
            int index = 0;
            // turn off all projectors except the current projector    
            foreach (IMyProjector projector in projectors)
            {
                projector.Enabled = (++index == currentProjector);
            }
        }

        private void Main(string argument, UpdateType updateSource)
        {
            if (argument.Length != 0)
            {
                if (argument.Equals("stop"))
                {
                    ChangeState(PrinterState.Resetting);
                }
                if (state == PrinterState.Incomplete)
                {
                    if (argument.Equals("start"))
                    {
                        ChangeState(PrinterState.Printing);
                    }
                }
                if (state == PrinterState.Ready)
                {
                    if (argument.Equals("start"))
                    {
                        ChangeState(PrinterState.Printing);
                    }
                    else if (argument.Equals("next"))
                    {
                        currentProjector++;
                        if (currentProjector > projectors.Count)
                        {
                            currentProjector = 1;
                        }
                        SwitchProjector();
                    }
                    else if (argument.Equals("prev"))
                    {
                        currentProjector--;
                        if (currentProjector < 1)
                        {
                            currentProjector = projectors.Count;
                        }
                        SwitchProjector();
                    }
                }
            }

            // check for conditions to goto next state    
            switch (state)
            {
                case PrinterState.Printing:
                    UpdateScreenWithRemainingPrintTime();
                    if (IsPistonAtMaximumPosition())
                    {
                        if (IsBluePrintCompleted())
                        {
                            ChangeState(PrinterState.Resetting);
                        }
                        else
                        {
                            ChangeState(PrinterState.Incomplete);
                        }
                    }
                    break;
                case PrinterState.Resetting:
                    if (IsPistonAtMinimumPosition())
                    {
                        ChangeState(PrinterState.Ready);
                    }
                    break;
                case PrinterState.Incomplete:
                    break;
                case PrinterState.Ready:
                    break;
                default:
                    break;
            }
        }

        private void ChangeState(PrinterState newState)
        {
            Echo($"Changing to State: {newState}");
            state = newState;
            switch (newState)
            {
                case PrinterState.Printing:
                    SetLights(true);
                    SetWelders(true);
                    ExtendPistons();
                    break;
                case PrinterState.Resetting:
                    UpdateScreenStatusOnly();
                    SetWelders(false);
                    RetractPistonsFast();
                    break;
                case PrinterState.Ready:
                    PlaySound(completionSound);
                    SetLights(false);
                    UpdateScreenWithPrintTime();
                    break;
                case PrinterState.Incomplete:
                    PlaySound(incompleteSound);
                    SetWelders(false);
                    RetractPistonsFast();
                    UpdateScreenWithIncompleteMessage();
                    break;
                default:
                    break;
            }

            Storage = state.ToString();
        }
    }
}
        // ================================================


// SCRIPT START ABOVE
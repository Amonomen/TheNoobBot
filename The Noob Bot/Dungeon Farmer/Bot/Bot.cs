﻿using System;
using System.Windows.Forms;
using nManager;
using nManager.FiniteStateMachine;
using nManager.Helpful;
using nManager.Wow.Bot.States;
using nManager.Wow.Class;
using nManager.Wow.Helpers;

namespace DungeonFarmer.Bot
{
    internal class Bot
    {
        private static readonly Engine Fsm = new Engine();

        internal static Spell SurveySpell;

        internal static bool Pulse()
        {
            try
            {
                SurveySpell = new Spell("Survey");
                if (!SurveySpell.KnownSpell)
                {
                    MessageBox.Show(Translate.Get(Translate.Id.Survey_spell_not_found__stopping_tnb));
                    Logging.Write("Survey spell not found, stopping bot.");
                    return false;
                }

                // Load CC:
                CombatClass.LoadCombatClass();

                // FSM
                Fsm.States.Clear();

                Fsm.AddState(new Pause {Priority = 100});
                Fsm.AddState(new Resurrect {Priority = 13});
                Fsm.AddState(new IsAttacked {Priority = 12});
                Fsm.AddState(new ToTown {Priority = 11});
                Fsm.AddState(new Looting {Priority = 10});
                Fsm.AddState(new Travel {Priority = 9});
                Fsm.AddState(new Regeneration {Priority = 8});
                Fsm.AddState(new SpecializationCheck {Priority = 7});
                Fsm.AddState(new LevelupCheck {Priority = 6});
                Fsm.AddState(new Trainers {Priority = 5});
                Fsm.AddState(new MillingState {Priority = 4});
                Fsm.AddState(new ProspectingState {Priority = 3});
                Fsm.AddState(new Farming {Priority = 2});
                Fsm.AddState(new ArchaeologyStates
                {
                    Priority = 1,
                    SolvingEveryXMin = DungeonFarmerSetting.CurrentSetting.SolvingEveryXMin,
                    MaxTryByDigsite = DungeonFarmerSetting.CurrentSetting.MaxTryByDigsite,
                    UseKeystones = DungeonFarmerSetting.CurrentSetting.UseKeystones
                });
                Fsm.AddState(new Idle {Priority = 0});

                Fsm.States.Sort();
                Fsm.StartEngine(5, "FSM DungeonFarmer");
                Archaeology.Initialize();
                return true;
            }
            catch (Exception e)
            {
                try
                {
                    Dispose();
                }
                catch
                {
                }
                Logging.WriteError("DungeonFarmer > Bot > Bot  > Pulse(): " + e);
                return false;
            }
        }

        internal static void Dispose()
        {
            try
            {
                CombatClass.DisposeCombatClass();
                Fsm.StopEngine();
                Fight.StopFight();
                MovementManager.StopMove();
            }
            catch (Exception e)
            {
                Logging.WriteError("DungeonFarmer > Bot > Bot  > Dispose(): " + e);
            }
        }
    }
}
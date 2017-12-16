﻿using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace JecsTools
{
    /// <summary>
    /// HumanlikeOrdersUtility
    /// 
    /// I wanted this class to exist so I can have all my float menu
    /// condition checks occur in one loop through objects, rather 
    /// than having a loop for every component, class, ability user,
    /// etc, which is intensive on hardware.
    /// 
    /// To do this, I've added an extra class (_Condition) and an enum
    /// (_ConditionType) to make the checks as simple as possible.
    /// 
    /// All conditions and their lists of actions (extra checks, etc)
    /// are loaded into FloatMenuOptionList;
    /// 
    /// Next, I've created two harmony patches to make a saved list
    /// of float menu options with a unique ID tag.
    /// 
    /// Using this system should lower 
    /// </summary>
    [StaticConstructorOnStartup]
    public static class _HumanlikeOrdersUtility
    {
        private static Dictionary<_Condition, List<Func<Vector3, Pawn, Thing, List<FloatMenuOption>>>> floatMenuOptionList;
        public static Dictionary<_Condition, List<Func<Vector3, Pawn, Thing, List<FloatMenuOption>>>> FloatMenuOptionList
        {
            get
            {
                if (floatMenuOptionList == null)
                {
                    DebugMessage("FMOL :: Initialized List");
                    floatMenuOptionList = new Dictionary<_Condition, List<Func<Vector3, Pawn, Thing, List<FloatMenuOption>>>>();
                }
                return floatMenuOptionList;
            }
        }

        private static bool debugMode = false;
        public static void DebugMessage(string s)
        {
            if (debugMode) Log.Message(s);
        }

        static _HumanlikeOrdersUtility()
        {
            DebugMessage("FMOL :: Initialized Constructor");
            optsID = "";
            lastOptsID = "1";
            foreach (Type current in typeof(FloatMenuPatch).AllSubclassesNonAbstract())
            {

                FloatMenuPatch item = (FloatMenuPatch)Activator.CreateInstance(current);

                DebugMessage("FMOL :: Enter Loop Step");
                IEnumerable<KeyValuePair<_Condition, Func<Vector3, Pawn, Thing, List<FloatMenuOption>>>> floatMenus = item.GetFloatMenus();
                DebugMessage("FMOL :: Float Menus Variable Declared");

                if (floatMenus != null && floatMenus.Count() > 0)
                {
                    DebugMessage("FMOL :: Float Menus Available Check Passed");
                    foreach (KeyValuePair<_Condition, Func<Vector3, Pawn, Thing, List<FloatMenuOption>>> floatMenu in floatMenus)
                    {
                        DebugMessage("FMOL :: Enter Float Menu Check Loop");
                        if (FloatMenuOptionList.ContainsKey(floatMenu.Key))
                        {
                            DebugMessage("FMOL :: Existing condition found for " + floatMenu.Key.ToString() + " adding actions to dictionary.");
                            FloatMenuOptionList[floatMenu.Key].Add(floatMenu.Value);
                        }
                        else
                        {
                            DebugMessage("FMOL :: Existing condition not found for " + floatMenu.Key.ToString() + " adding key and actions to dictionary.");
                            FloatMenuOptionList.Add(floatMenu.Key, new List<Func<Vector3, Pawn, Thing, List<FloatMenuOption>>>() { floatMenu.Value });
                        }
                    }
                }
            }

            HarmonyInstance harmony = HarmonyInstance.Create("rimworld.jecrell.humanlikeorders");
            harmony.Patch(AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders"), null, new HarmonyMethod(typeof(_HumanlikeOrdersUtility), nameof(AddHumanlikeOrders_PostFix)));
            //harmony.Patch(AccessTools.Method(typeof(FloatMenuMakerMap), "CanTakeOrder"), null, new HarmonyMethod(typeof(_HumanlikeOrdersUtility), nameof(CanTakeOrder_PostFix)));
        }

        //private static int floatMenuID = 0;
        //private static int lastSavedFloatMenuID = -1;
        private static List<FloatMenuOption> savedList = new List<FloatMenuOption>();
        public static string optsID = "";
        public static string lastOptsID = "1";


        //private static void CanTakeOrder_PostFix(Pawn pawn, ref bool __result)
        //{
        //    if (__result)
        //    {
        //        floatMenuID = Rand.Range(1000, 9999);
        //        DebugMessage("FMOL :: ID Set to " + floatMenuID);
        //    }
        //}


        // RimWorld.FloatMenuMakerMap
        public static void AddHumanlikeOrders_PostFix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            IntVec3 c = IntVec3.FromVector3(clickPos);
            optsID = "";
            optsID += pawn.ThingID;
            optsID += c.ToString();
            for (int i = 0; i < (opts?.Count() ?? 0); i++)
            {
                optsID += opts[i].Label;
            }
            if (optsID == lastOptsID)
            {
                opts.AddRange(savedList);
                return;
            }
            DebugMessage("FMOL :: New list constructed");
            DebugMessage(optsID);
            lastOptsID = optsID;
            savedList.Clear();
            if (c.GetThingList(pawn.Map) is List<Thing> things && !things.NullOrEmpty())
            {
                foreach (KeyValuePair<_Condition, List<Func<Vector3, Pawn, Thing, List<FloatMenuOption>>>> pair in FloatMenuOptionList)
                {
                    if (!pair.Value.NullOrEmpty())
                    {
                        List<Thing> passers = things.FindAll(x => pair.Key.Passes(x));
                        if (!passers.NullOrEmpty())
                        {
                            foreach (Thing passer in passers)
                            {
                                foreach (Func<Vector3, Pawn, Thing, List<FloatMenuOption>> func in pair.Value)
                                {
                                    if (func.Invoke(clickPos, pawn, passer) is List<FloatMenuOption> newOpts && !newOpts.NullOrEmpty())
                                    {
                                        opts.AddRange(newOpts);
                                        savedList.AddRange(newOpts);
                                    }
                                }
                            }
                        }
                    }
                }
            }


        }
    }


    public enum _ConditionType
    {
        IsType,
        IsTypeStringMatch,
        ThingHasComp,
        HediffHasComp
    }

    public struct _Condition : IEquatable<_Condition>
    {
        public _ConditionType Condition;
        public object Data;

        public _Condition(_ConditionType condition, object data)
        {
            this.Condition = condition;
            this.Data = data;
        }

        public override string ToString()
        {
            return "Condition_" + Condition.ToString() + "_" + Data.ToString();
        }

        public bool Equals(_Condition other)
        {
            return this.Data == other.Data && this.Condition == other.Condition;
        }

        public bool Passes(object toCheck)
        {
            switch (Condition)
            {
                case _ConditionType.IsType:
                    //Log.Message(toCheck.GetType().ToString());
                    //Log.Message(Data.ToString());

                    //////////////////////////
                    ///PSYCHOLOGY SPECIAL CASE
                    if (toCheck.GetType().ToString() == "Psychology.PsychologyPawn" && Data.ToString() == "Verse.Pawn")
                    {
                        return true;
                    }
                    //////////////////////////
                    if (toCheck.GetType() == Data.GetType() || (toCheck.GetType() == Data || Data.GetType().IsAssignableFrom(toCheck.GetType())))
                        return true;
                    break;
                case _ConditionType.IsTypeStringMatch:
                    if (toCheck.GetType().ToString() == (string)toCheck)
                        return true;
                    break;
                case _ConditionType.ThingHasComp:
                    var dataType = Data.GetType();
                    if (toCheck is ThingWithComps t && t.AllComps.FirstOrDefault(x => x.GetType() == dataType) != null)
                        return true;
                    break;
            }
            return false;
        }
    }

    public abstract class FloatMenuPatch
    {
        public abstract IEnumerable<KeyValuePair<_Condition, Func<Vector3, Pawn, Thing, List<FloatMenuOption>>>> GetFloatMenus();
    }


}

using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using KMod;
using STRINGS;
using UnityEngine;
using UnityEngine.UI;

namespace CarSkim
{
	public class CarSkim : UserMod2
	{
		public override void OnLoad(Harmony harmony)
		{
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(CO2ScrubberConfig), "ConfigureBuildingTemplate")]
		private static class CO2_Patch
		{
			private static void Postfix(GameObject go, Tag prefab_tag)
			{
				Strings.Get(BUILDINGS.PREFABS.CO2SCRUBBER.EFFECT.key).String = string.Concat(new string[]
				{
					"Uses ",
					UI.FormatAsLink("Hydrogen", "HYDROGEN"),
					" to refine ",
					UI.FormatAsLink("Carbon Dioxide", "CARBONDIOXIDE"),
					" from the air. \n Producing hot ",
					UI.FormatAsLink("Methane", "METHANE"),
					" and ",
					UI.FormatAsLink("Water", "STEAM")
				});
				go.AddOrGet<LoopingSounds>();
				go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery, false);
				go.AddOrGet<Storage>().SetDefaultStoredItemModifiers(Storage.StandardInsulatedStorage);
				Storage storage = go.AddComponent<Storage>();
				storage.showInUI = false;
				storage.capacityKg = 10f;
				storage.SetDefaultStoredItemModifiers(Storage.StandardInsulatedStorage);
				go.AddOrGet<AirFilter>().filterTag = GameTagExtensions.Create(SimHashes.Hydrogen);
				PassiveElementConsumer passiveElementConsumer = go.AddOrGet<PassiveElementConsumer>();
				passiveElementConsumer.elementToConsume = SimHashes.CarbonDioxide;
				passiveElementConsumer.consumptionRate = 2f;
				passiveElementConsumer.capacityKG = 4f;
				passiveElementConsumer.consumptionRadius = 4;
				passiveElementConsumer.showInStatusPanel = true;
				passiveElementConsumer.sampleCellOffset = new Vector3(0f, 0f, 0f);
				passiveElementConsumer.isRequired = false;
				passiveElementConsumer.storeOnConsume = true;
				passiveElementConsumer.showDescriptor = false;
				passiveElementConsumer.ignoreActiveChanged = true;
				ElementConverter elementConverter = go.AddOrGet<ElementConverter>();
				elementConverter.consumedElements = new ElementConverter.ConsumedElement[]
				{
					new ElementConverter.ConsumedElement(GameTagExtensions.Create(SimHashes.Hydrogen), 0.38f, true),
					new ElementConverter.ConsumedElement(GameTagExtensions.Create(SimHashes.CarbonDioxide), 0.85f, true)
				};
				elementConverter.outputElements = new ElementConverter.OutputElement[]
				{
					new ElementConverter.OutputElement(0.4f, SimHashes.Methane, 393.15f, false, true, 0f, 0f, 0f, byte.MaxValue, 0, true),
					new ElementConverter.OutputElement(0.6f, SimHashes.Steam, 383.15f, false, true, 0f, 0f, 0f, byte.MaxValue, 0, true)
				};
				ConduitConsumer conduitConsumer = go.AddOrGet<ConduitConsumer>();
				conduitConsumer.conduitType = ConduitType.Gas;
				conduitConsumer.consumptionRate = 0.5f;
				conduitConsumer.capacityKG = 5f;
				conduitConsumer.capacityTag = ElementLoader.FindElementByHash(SimHashes.Hydrogen).tag;
				conduitConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Store;
				conduitConsumer.forceAlwaysSatisfied = true;
				ConduitDispenser conduitDispenser = go.AddOrGet<ConduitDispenser>();
				conduitDispenser.conduitType = ConduitType.Gas;
				conduitDispenser.invertElementFilter = false;
				conduitDispenser.elementFilter = new SimHashes[]
				{
					SimHashes.Steam,
					SimHashes.Methane
				};
				conduitDispenser.storage = storage;
				go.AddOrGet<KBatchedAnimController>().randomiseLoopedOffset = true;
			}
		}

		[HarmonyPatch(typeof(CO2ScrubberConfig), "CreateBuildingDef")]
		private class CO2_Heat_Patch
		{
			private static BuildingDef Postfix(BuildingDef __result)
			{
				__result.EnergyConsumptionWhenActive = 300f;
				__result.SelfHeatKilowattsWhenActive = 1.525f;
				__result.InputConduitType = ConduitType.Gas;
				__result.OutputConduitType = ConduitType.Gas;
				__result.OverheatTemperature = 355.25f;
				return __result;
			}
		}

		[HarmonyPatch(typeof(Storage), "Store")]
		private static class Storage_Patch
		{
			private static void Postfix(GameObject go, Storage __instance)
			{
				if (__instance.GetComponent<KPrefabID>().name.ToLower().Contains("co2scrubber") && !__instance.GetComponent<KPrefabID>().name.ToLower().Contains("construction"))
				{
					Storage[] componentsInParent = __instance.GetComponentsInParent<Storage>();
					float num = (float)Traverse.Create(Game.Instance.gasConduitFlow).Field("MaxMass").GetValue();
					if (__instance == componentsInParent[0])
					{
						if (__instance.GetAmountAvailable(SimHashes.Steam.CreateTag()) > num)
						{
							__instance.Transfer(componentsInParent[1], SimHashes.Steam.CreateTag(), num, false, true);
						}
						__instance.Transfer(componentsInParent[1], SimHashes.Methane.CreateTag(), go.GetComponent<PrimaryElement>().Mass, false, true);
					}
				}
			}
		}
		
		[HarmonyPatch(typeof(ModsScreen), "BuildDisplay")]
		private static class ModsScreen_BuildDisplay_Patch
		{
			private static void Postfix(KButton ___workshopButton, GameObject ___entryPrefab, Transform ___entryParent, IList ___displayedMods)
			{
				FieldInfo field = typeof(ModsScreen).GetNestedType("DisplayedMod", BindingFlags.Public | BindingFlags.NonPublic).GetField("mod_index", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo field2 = typeof(ModsScreen).GetNestedType("DisplayedMod", BindingFlags.Public | BindingFlags.NonPublic).GetField("rect_transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (object obj in ___displayedMods)
				{
					if (Global.Instance.modManager.mods[(int)field.GetValue(obj)].staticID == "CritterDrop")
					{
						Util.KInstantiateUI<HierarchyReferences>(___entryPrefab, ___entryParent.gameObject, false);
						RectTransform rectTransform = (RectTransform)((field2 != null) ? field2.GetValue(obj) : null);
						KButton kbutton = Util.KInstantiateUI<KButton>(___workshopButton.gameObject, rectTransform.gameObject, false);
						kbutton.name = "myButton";
						kbutton.GetComponentInChildren<LocText>().text = "Settings";
						kbutton.GetComponent<LayoutElement>().preferredWidth = 70f;
						kbutton.gameObject.SetActive(true);
						kbutton.onClick += delegate()
						{
						};
					}
				}
			}
		}

		public struct DisplayedMod
		{
			public RectTransform rect_transform;

			public int mod_index;
		}
	}
}

using System;
using System.Reflection;
using ColossalFramework;
using ICities;
using TrafficManager.Custom.AI;
using TrafficManager.Custom.Manager;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using TrafficManager.Custom.Misc;
using TrafficManager.State;
using ColossalFramework.UI;

namespace TrafficManager {
    public class LoadingExtension : LoadingExtensionBase {
        public static LoadingExtension Instance;
        public static bool IsPathManagerCompatible = true;
		public static bool IsTrafficPlusPlusLoaded = false;
		public static bool IsPathManagerReplaced = false;
		public CustomPathManager CustomPathManager { get; set; }
        public bool DespawnEnabled { get; set; }
        public bool DetourInited { get; set; }
        public bool NodeSimulationLoaded { get; set; }
		public MethodInfo[] OriginalMethods { get; set; }
		public MethodInfo[] CustomMethods { get; set; }
		public RedirectCallsState[] CustomRedirects { get; set; }
        public TrafficManagerMode ToolMode { get; set; }
        public TrafficLightTool TrafficLightTool { get; set; }
        public UIBase UI { get; set; }

        public LoadingExtension() {
        }

		public void revertDetours() {
			if (LoadingExtension.Instance.DetourInited) {
				Log.Warning("Revert detours");
				for (int i = 0; i < 8; ++i) {
					if (LoadingExtension.Instance.OriginalMethods[i] != null)
						RedirectionHelper.RevertRedirect(LoadingExtension.Instance.OriginalMethods[i], LoadingExtension.Instance.CustomRedirects[i]);
				}
				LoadingExtension.Instance.DetourInited = false;
			}
		}

		public void initDetours() {
			Log.Warning("Init detours");
			if (!LoadingExtension.Instance.DetourInited) {
				bool detourFailed = false;
				Log.Message("Redirecting Car AI Calculate Segment Calls");
				try {

					LoadingExtension.Instance.OriginalMethods[0] = typeof(CarAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
								typeof (PathUnit.Position), typeof (uint), typeof (byte), typeof (PathUnit.Position),
								typeof (uint), typeof (byte), typeof (Vector3).MakeByRefType(),
								typeof (Vector3).MakeByRefType(), typeof (float).MakeByRefType()
							},
							null);
					LoadingExtension.Instance.CustomMethods[0] = typeof(CustomCarAI).GetMethod("TmCalculateSegmentPosition");
					LoadingExtension.Instance.CustomRedirects[0] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[0], LoadingExtension.Instance.CustomMethods[0]);
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::CalculateSegmentPosition.");
					detourFailed = true;
				}

				Log.Message("Redirecting SimulationStep");
				try {
					LoadingExtension.Instance.OriginalMethods[1] = typeof(RoadBaseAI).GetMethod("SimulationStep", new[] { typeof(ushort), typeof(NetNode).MakeByRefType() });
					LoadingExtension.Instance.CustomMethods[1] = typeof(CustomRoadAI).GetMethod("CustomNodeSimulationStep");
					LoadingExtension.Instance.CustomRedirects[1] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[1], LoadingExtension.Instance.CustomMethods[1]);
				} catch (Exception) {
					Log.Error("Could not redirect RoadBaseAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Message("Redirecting SimulationStep");
				try {
					LoadingExtension.Instance.OriginalMethods[2] = typeof(RoadBaseAI).GetMethod("SimulationStep", new[] { typeof(ushort), typeof(NetSegment).MakeByRefType() });
					LoadingExtension.Instance.CustomMethods[2] = typeof(CustomRoadAI).GetMethod("CustomSegmentSimulationStep");
					LoadingExtension.Instance.CustomRedirects[2] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[2], LoadingExtension.Instance.CustomMethods[2]);
				} catch (Exception) {
					Log.Error("Could not redirect RoadBaseAI::SimulationStep.");
				}

				Log.Message("Redirecting Human AI Calls");
				try {
					LoadingExtension.Instance.OriginalMethods[3] = typeof(HumanAI).GetMethod("CheckTrafficLights",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[] { typeof(ushort), typeof(ushort) },
							null);
					LoadingExtension.Instance.CustomMethods[3] = typeof(CustomHumanAI).GetMethod("CustomCheckTrafficLights");
					LoadingExtension.Instance.CustomRedirects[3] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[3], LoadingExtension.Instance.CustomMethods[3]);
				} catch (Exception) {
					Log.Error("Could not redirect HumanAI::CheckTrafficLights.");
					detourFailed = true;
				}

				Log.Message("Redirecting CarAI Simulation Step Calls");
				try {
					LoadingExtension.Instance.OriginalMethods[4] = typeof(CarAI).GetMethod("SimulationStep",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3)
								});
					LoadingExtension.Instance.CustomMethods[4] = typeof(CustomCarAI).GetMethod("TrafficManagerSimulationStep");
					LoadingExtension.Instance.CustomRedirects[4] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[4], LoadingExtension.Instance.CustomMethods[4]);
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Message("Redirecting PassengerCarAI Simulation Step Calls");
				try {
					LoadingExtension.Instance.OriginalMethods[5] = typeof(PassengerCarAI).GetMethod("SimulationStep",
							new[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) });
					LoadingExtension.Instance.CustomMethods[5] = typeof(CustomPassengerCarAI).GetMethod("CustomSimulationStep");
					LoadingExtension.Instance.CustomRedirects[5] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[5], LoadingExtension.Instance.CustomMethods[5]);
				} catch (Exception) {
					Log.Error("Could not redirect PassengerCarAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Message("Redirecting CargoTruckAI Simulation Step Calls");
				try {
					LoadingExtension.Instance.OriginalMethods[6] = typeof(CargoTruckAI).GetMethod("SimulationStep",
								new[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) });
					LoadingExtension.Instance.CustomMethods[6] = typeof(CustomCargoTruckAI).GetMethod("CustomSimulationStep");
					LoadingExtension.Instance.CustomRedirects[6] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[6], LoadingExtension.Instance.CustomMethods[6]);
				} catch (Exception) {
					Log.Error("Could not redirect CargoTruckAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Message("Redirection CarAI Calculate Segment Position calls for non-Traffic++");
				try {
					LoadingExtension.Instance.OriginalMethods[7] = typeof(CarAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
								typeof (uint),
								typeof (byte), typeof (Vector3).MakeByRefType(), typeof (Vector3).MakeByRefType(),
								typeof (float).MakeByRefType()
							},
							null);
					LoadingExtension.Instance.CustomMethods[7] = typeof(CustomCarAI).GetMethod("TmCalculateSegmentPositionPathFinder");
					LoadingExtension.Instance.CustomRedirects[7] =
						RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[7], LoadingExtension.Instance.CustomMethods[7]);
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::CalculateSegmentPosition");
					detourFailed = true;
				}

				Log.Message("Redirecting TrainAI Simulation Step Calls");
				try {
					LoadingExtension.Instance.OriginalMethods[8] = typeof(TrainAI).GetMethod("SimulationStep",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3)
								});
					LoadingExtension.Instance.CustomMethods[8] = typeof(CustomTrainAI).GetMethod("TrafficManagerSimulationStep");
					LoadingExtension.Instance.CustomRedirects[8] = RedirectionHelper.RedirectCalls(LoadingExtension.Instance.OriginalMethods[8], LoadingExtension.Instance.CustomMethods[8]);
				} catch (Exception) {
					Log.Error("Could not redirect TrainAI::SimulationStep.");
					detourFailed = true;
				}

				if (detourFailed) {
					UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Incompatibility Issue", "Traffic Manager: President Edition detected an incompatibility with another mod! You can continue playing but it's NOT recommended. Traffic Manager will not work as expected.", true);
				}

				LoadingExtension.Instance.DetourInited = true;
			}
		}

        public override void OnCreated(ILoading loading) {
            SelfDestruct.DestructOldInstances(this);

            base.OnCreated(loading);

            Log.Message("Setting ToolMode");
            ToolMode = TrafficManagerMode.None;

            Log.Message("Init RevertMethods");
			OriginalMethods = new MethodInfo[9];
			CustomMethods = new MethodInfo[9];
			CustomRedirects = new RedirectCallsState[9];

            Log.Message("Setting Despawn to False");
            DespawnEnabled = true;

            Log.Message("Init DetourInited");
            DetourInited = false;

            Log.Message("Init Custom PathManager");
            CustomPathManager = new CustomPathManager();
        }

        public override void OnReleased() {
            base.OnReleased();

            if (ToolMode != TrafficManagerMode.None) {
                ToolMode = TrafficManagerMode.None;
                DestroyTool();
            }
        }

        public override void OnLevelLoaded(LoadMode mode) {
            Log.Warning("OnLevelLoaded calling base method");
            base.OnLevelLoaded(mode);
            Log.Message("OnLevelLoaded Returned from base, calling custom code.");

            switch (mode) {
                case LoadMode.NewGame:
                    OnNewGame();
                    break;
                case LoadMode.LoadGame:
                    OnLoaded();
                    break;
            }

            if (mode == LoadMode.NewGame || mode == LoadMode.LoadGame) {
				determinePathManagerCompatible();

                Instance = this;
				initDetours();

                if (IsPathManagerCompatible && ! IsPathManagerReplaced) {
					try {
						Log.Warning("#####################################");

						Log.Message("Pathfinder Compatible. Setting up CustomPathManager and SimManager.");
						var pathManagerInstance = typeof(Singleton<PathManager>).GetField("sInstance", BindingFlags.Static | BindingFlags.NonPublic);

						var stockPathManager = PathManager.instance;
						Log.Message($"Got stock PathManager instance {stockPathManager.GetName()}");

						CustomPathManager = stockPathManager.gameObject.AddComponent<CustomPathManager>();
						Log.Message("Added CustomPathManager to gameObject List");

						if (CustomPathManager == null) {
							Log.Error("CustomPathManager null. Error creating it.");
							return;
						}

						CustomPathManager.UpdateWithPathManagerValues(stockPathManager);
						Log.Message("UpdateWithPathManagerValues success");

						pathManagerInstance?.SetValue(null, CustomPathManager);

						Log.Message("Getting Current SimulationManager");
						var simManager =
							typeof(SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)?
								.GetValue(null) as FastList<ISimulationManager>;

						Log.Message("Removing Stock PathManager");
						simManager?.Remove(stockPathManager);

						Log.Message("Adding Custom PathManager");
						simManager?.Add(CustomPathManager);

						Object.Destroy(stockPathManager, 10f);

						IsPathManagerReplaced = true;
					} catch (Exception) {
						UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Incompatibility Issue", "Traffic Manager: President Edition detected an incompatibility with another mod! You can continue playing but it's NOT recommended. Traffic Manager will not work as expected.", true);
						IsPathManagerCompatible = false;
					}
				}

                Log.Message("Adding Controls to UI.");
                UI = ToolsModifierControl.toolController.gameObject.AddComponent<UIBase>();
                TrafficPriority.LeftHandDrive = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic ==
                                                SimulationMetaData.MetaBool.True;
				TrafficPriority.OnLevelLoading();
				CustomRoadAI.OnLevelLoading();
			}
        }

		private void determinePathManagerCompatible() {
			IsPathManagerCompatible = true;
			if (!IsPathManagerReplaced) {

				var loadingWrapperLoadingExtensionsField = typeof(LoadingWrapper).GetField("m_LoadingExtensions", BindingFlags.NonPublic | BindingFlags.Instance);
				List<ILoadingExtension> loadingExtensions = null;
				if (loadingWrapperLoadingExtensionsField != null) {
					loadingExtensions = (List<ILoadingExtension>) loadingWrapperLoadingExtensionsField.GetValue(Singleton<LoadingManager>.instance.m_LoadingWrapper);
				} else {
					Log.Message("Could not get loading extensions field");
				}

				if (loadingExtensions != null) {
					Log.Message("Loaded extensions:");
					foreach (ILoadingExtension extension in loadingExtensions) {
						if (extension.GetType().Namespace == null)
							continue;

						Log.Message($"type: {extension.GetType().ToString()} type namespace: {extension.GetType().Namespace.ToString()} toString: {extension.ToString()}");
						var namespaceStr = extension.GetType().Namespace.ToString();
						if ("Improved_AI".Equals(namespaceStr)) {
							IsPathManagerCompatible = false; // Improved AI found
						}
					}
				} else {
					Log.Message("Could not get loading extensions");
				}

				if (Singleton<PathManager>.instance.GetType() != typeof(PathManager)) {
					Log.Message("PathManager manipulation detected. Disabling custom PathManager " + Singleton<PathManager>.instance.GetType().ToString());
					IsPathManagerCompatible = false;
				}
			}

			if (!IsPathManagerCompatible) {
				Options.setAdvancedAI(false);
			}
		}

		public override void OnLevelUnloading() {
            base.OnLevelUnloading();
			if (Instance == null)
				Instance = this;
			revertDetours();

			try {
				TrafficPriority.OnLevelUnloading();
				CustomCarAI.OnLevelUnloading();
				CustomRoadAI.OnLevelUnloading();
				TrafficLightsManual.OnLevelUnloading();
				TrafficLightsTimed.OnLevelUnloading();
				Flags.OnLevelUnloading();
                Translation.OnLevelUnloading();

				if (Instance != null)
					Instance.NodeSimulationLoaded = false;
            } catch (Exception e) {
                Log.Error("Exception unloading mod. " + e.Message);
                // ignored - prevents collision with other mods
            }
        }

        protected virtual void OnNewGame() {
            Log.Message("New Game Started");
        }

        protected virtual void OnLoaded() {
            Log.Message("Loaded save game.");
        }

        public void SetToolMode(TrafficManagerMode mode) {
            if (mode == ToolMode) return;

            //UI.toolMode = mode;
            ToolMode = mode;

            if (mode != TrafficManagerMode.None) {
                DestroyTool();
                EnableTool();
            } else {
                DestroyTool();
            }
        }

        public void EnableTool() {
            if (TrafficLightTool == null) {
                TrafficLightTool = ToolsModifierControl.toolController.gameObject.GetComponent<TrafficLightTool>() ??
                                   ToolsModifierControl.toolController.gameObject.AddComponent<TrafficLightTool>();
            }

            ToolsModifierControl.toolController.CurrentTool = TrafficLightTool;
            ToolsModifierControl.SetTool<TrafficLightTool>();
        }

        private void DestroyTool() {
            if (TrafficLightTool != null) {
                ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
                ToolsModifierControl.SetTool<DefaultTool>();

                Object.Destroy(TrafficLightTool);
                TrafficLightTool = null;
            }
        }
	}
}

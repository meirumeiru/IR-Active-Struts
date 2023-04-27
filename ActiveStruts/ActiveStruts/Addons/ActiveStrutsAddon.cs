using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActiveStruts.Modules;
using ActiveStruts.Util;
using UnityEngine;
using Utilities = ActiveStruts.Util.Utilities;

namespace ActiveStruts.Addons
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ActiveStrutsFlight : ActiveStrutsAddon
	{
		public override string AddonName { get { return this.name; } }
	}

	[KSPAddon(KSPAddon.Startup.EditorAny, false)]
	public class ActiveStrutsEditor : ActiveStrutsAddon
	{
		public override string AddonName { get { return this.name; } }
	}

	public class ActiveStrutsAddon : MonoBehaviour
	{
// FEHLER, public ist temp, weil... das kann total private sein... echt jetzt
		public static AddonMode Mode { get; set; }
		public static ModuleIRActiveStrut_v3 CurrentTargeter { get; set; }

		private static List<ModuleIRActiveStrutTarget_v3> targetHighlightedParts;

// FEHLER, Daten sind noch ungeprüft ab hier

		public virtual String AddonName { get; set; }
		private const float MP_TO_RAY_HIT_DISTANCE_TOLERANCE = 0.02f;
		private const int UNUSED_TARGET_PART_REMOVAL_COUNTER_INTERVAL = 18000;
		private static GameObject connector;
		private static object idResetQueueLock;
		private static object targetDeleteListLock;
		private static int idResetCounter;
		private static int unusedTargetPartRemovalCounter;
		private static bool idResetTrimFlag;
		private static bool noInputAxesReset;
		private static bool partPlacementInProgress;
		private static Queue<IDResetable> idResetQueue;
		private HashSet<MouseOverHighlightData> mouseOverPartsData;
		private object mouseOverSetLock;
		private bool resetAllHighlighting;


		public static bool FlexibleAttachActive { get; set; }
		public static Part NewSpawnedPart { get; set; }
		public static Vector3 Origin { get; set; }

		////////////////////////////////////////
		// Functions

		static public void StartLink(ModuleIRActiveStrut_v3 p_Targeter)
		{
			Mode = AddonMode.Link;
			CurrentTargeter = p_Targeter;

			InputLockManager.SetControlLock(Config.Instance.EditorInputLockId);

			if(Config.Instance.ShowHelpTexts)
				OSD.PostMessage(Config.Instance.LinkHelpText, 5);

			ModuleIRActiveStrutTarget_v3[] aStruts = FlightGlobals.FindObjectsOfType<ModuleIRActiveStrutTarget_v3>();

			for(int i = 0; i < aStruts.Length; i++)
			{
				if(aStruts[i].IsValidTarget)
				{
					aStruts[i].part.SetHighlightColor(Color.cyan);
					aStruts[i].part.SetHighlight(true, false);
					aStruts[i].part.SetHighlightType(Part.HighlightType.AlwaysOn);

					targetHighlightedParts.Add(aStruts[i]);
				}
			}
		}

		static public void AbortLink()
		{
			Mode = AddonMode.None;
			CurrentTargeter.RemoveLink();
			CurrentTargeter = null;

			InputLockManager.RemoveControlLock(Config.Instance.EditorInputLockId);

			foreach(ModuleIRActiveStrutTarget_v3 p in targetHighlightedParts)
				p.part.SetHighlightDefault();

			targetHighlightedParts.Clear();
		}

		static public void LinkBuilt()
		{
			Mode = AddonMode.None;
			CurrentTargeter = null;

			InputLockManager.RemoveControlLock(Config.Instance.EditorInputLockId);

			foreach(ModuleIRActiveStrutTarget_v3 p in targetHighlightedParts)
				p.part.SetHighlightDefault();

			targetHighlightedParts.Clear();
		}

// FEHLER, ab hier ungeprüftes Zeugs

		public void Awake()
		{
			if(!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
				return;

			targetHighlightedParts = new List<ModuleIRActiveStrutTarget_v3>();

			unusedTargetPartRemovalCounter = HighLogic.LoadedSceneIsEditor ? 30 : 180;
			FlexibleAttachActive = false;
			mouseOverSetLock = new object();
			lock (mouseOverSetLock)
			{
				mouseOverPartsData = new HashSet<MouseOverHighlightData>();
			}
			connector = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			connector.name = "ASConn";
			DestroyImmediate(connector.GetComponent<Collider>());
			var connDim = Config.Instance.ConnectorDimension;
			connector.transform.localScale = new Vector3(connDim, connDim, connDim);
			var mr = connector.GetComponent<MeshRenderer>();
			mr.name = "ASConn";
			mr.material = new Material(Shader.Find("Transparent/Diffuse"))
			{
				color = Color.green.MakeColorTransparent(Config.Instance.ColorTransparency)
			};
			connector.SetActive(false);

			Mode = AddonMode.None;
			if(HighLogic.LoadedSceneIsEditor)
			{
				GameEvents.onPartRemove.Add(HandleEditorPartDetach);
				GameEvents.onPartAttach.Add(HandleEditorPartAttach);
				targetDeleteListLock = new object();
			}
			else if(HighLogic.LoadedSceneIsFlight)
			{
				GameEvents.onPartAttach.Add(HandleFlightPartAttach);
				GameEvents.onPartRemove.Add(HandleFlightPartAttach);
				idResetQueueLock = new object();
				idResetQueue = new Queue<IDResetable>(10);
				idResetCounter = Config.ID_RESET_CHECK_INTERVAL;
				idResetTrimFlag = false;
			}
		}

/* FEHLER, wollen wir nicht mehr...sag ich jetzt mal, verdammt
		private static GameObject CreateStraightOutHintForPart(ModuleActiveStrut module)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			go.SetActive(false);
			go.name = Guid.NewGuid().ToString();
			DestroyImmediate(go.GetComponent<Collider>());
			var connDim = Config.Instance.ConnectorDimension;
			go.transform.localScale = new Vector3(connDim, connDim, connDim);
			var mr = go.GetComponent<MeshRenderer>();
			mr.name = go.name;
			mr.material = new Material(Shader.Find("Transparent/Diffuse"))
			{
				color = Color.blue.MakeColorTransparent(Config.Instance.ColorTransparency)
			};
			//Debug.Log ("[IRAS] creating hint, color transparency:" + Config.Instance.ColorTransparency);
			UpdateStraightOutHint(module, go);
			return go;
		}
*/
		public static IDResetable Dequeue()
		{
			lock (idResetQueueLock)
			{
				return idResetQueue.Dequeue();
			}
		}

		public static void Enqueue(IDResetable module)
		{
			lock (idResetQueueLock)
			{
				idResetQueue.Enqueue(module);
			}
		}

		//public void FixedUpdate()
		//{
		//	if(!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
		//	{
		//		return;
		//	}

		//}

		private void Update_ClickToNowhere()
		{
			connector.SetActive(false);
	
			if(Input.GetKeyDown(KeyCode.Mouse0))
			{
				AbortLink();
			}
			else if((Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape)))
			{
				Input.ResetInputAxes(); // ignore the current input, we already handled it

				AbortLink();
			}
		}

		public void Update()
		{
				if(HighLogic.LoadedSceneIsFlight)
				{
					if(NewSpawnedPart != null)
					{
						if(CurrentTargeter != null)
							NewSpawnedPart.transform.position = CurrentTargeter.transform_().position;
						else
						{
							var module = NewSpawnedPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget];

							Destroy(NewSpawnedPart);
							NewSpawnedPart = null;
						}
					}
					_processIdResets();
				}

			if((Mode == AddonMode.Link) && (CurrentTargeter != null))
			{
				ActiveStruts.Util.Tuple<Vector3, RaycastHit?> mp = Utilities.GetMouseWorldPosition();
				if(mp != null)
				{
					var raycast = Utilities.PerformRaycast(
						CurrentTargeter.Origin_().position, mp.Item1, CurrentTargeter.RealModelForward);

					PointToMousePosition(mp.Item1, raycast);

					if(!raycast.HitResult)
					{
						Update_ClickToNowhere();
						return;
					}

					var mr = connector.GetComponent<MeshRenderer>();

// if(raycast.HittedPart == null) FEHLER, das darf doch gar nicht sein, oder?...

					var validPos = raycast.HitResult
								&& raycast.HittedPart != null
								&& (raycast.HittedPart.vessel != null || HighLogic.LoadedSceneIsEditor)
								&& raycast.DistanceFromOrigin <= Config.Instance.MaxDistance
								&& (raycast.RayAngle <= Config.Instance.MaxAngle);

					if(validPos)
					{
						ModuleIRActiveStrut_v3 hittedActiveStrut = raycast.HittedPart.gameObject.GetComponent<ModuleIRActiveStrut_v3>();

	//				var tPos = CurrentTargeter.Origin_().position;
	//				var mp.Item1osDist = Vector3.Distance(tPos, mp.Item1);
	//				valid &= Mathf.Abs(mp.Item1osDist - raycast.DistanceFromOrigin) < mp.Item1_TO_RAY_HIT_DISTANCE_TOLERANCE;

						mr.material.color = (hittedActiveStrut != null ? Color.green : Color.yellow).MakeColorTransparent(Config.Instance.ColorTransparency);
					}
					else
						mr.material.color = Color.red.MakeColorTransparent(Config.Instance.ColorTransparency);


					if(Input.GetKeyDown(KeyCode.Mouse0))
					{
							// FEHLER, gleich wie oben, nicht gut -> zusammenfassen oder sowas
						ModuleIRActiveStrutTarget_v3 hittedActiveStrut = raycast.HittedPart.gameObject.GetComponent<ModuleIRActiveStrutTarget_v3>();

						if(hittedActiveStrut != null)
							CurrentTargeter.SetLink(hittedActiveStrut);
						else
							ProcessFreeAttachPlacement(raycast);

						connector.SetActive(false);
	
						Input.ResetInputAxes(); // ignore the current input, we already handled it

						LinkBuilt();
					}
					else if((Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape)))
					{
						Input.ResetInputAxes(); // ignore the current input, we already handled it

						AbortLink();
					}
				}
				else
					Update_ClickToNowhere();
			}

			//if run in FixedUpdate transforms are offset in orbit around bodies without atmosphere -> FEHLER, hä???
			ProcessFixedUpdate();
		}

		private void HandleEditorPartAttach(GameEvents.HostTargetAction<Part, Part> data)
		{
			var partList = new List<Part> {data.host};
			foreach(var child in data.host.children)
			{
				child.RecursePartList(partList);
			}
			if(!data.host.name.Contains("ASTargetCube") || Mode != AddonMode.FreeAttach)
			{
				return;
			}
	//		CurrentTargeter.PlaceFreeAttach(data.host);
			NewSpawnedPart = null;
		}

		private void HandleEditorPartDetach(GameEvents.HostTargetAction<Part, Part> hostTargetAction)
		{
/*			var partList = new List<Part> {hostTargetAction.target};
			foreach(var child in hostTargetAction.target.children)
			{
				child.RecursePartList(partList);
			}
			var movedModules = (from p in partList
				where p.Modules.Contains(Config.Instance.ModuleName)
				select p.Modules[Config.Instance.ModuleName] as ModuleActiveStrut).ToList();
			var vesselModules = (from p in Utilities.ListEditorParts(false)
				where p.Modules.Contains(Config.Instance.ModuleName)
				select p.Modules[Config.Instance.ModuleName] as ModuleActiveStrut).ToList();
			foreach(var module in movedModules)
			{
				module.Unlink();
			}
			foreach(var module in vesselModules.Where(module =>
				(module.Target != null && movedModules.Any(m => m.ID == module.Target.ID) ||
				 (module.Targeter != null && movedModules.Any(m => m.ID == module.Targeter.ID)))))
			{
				module.Unlink();
			}*/
		}

		//private void HandleEvaStart(GameEvents.FromToAction<Part, Part> data)
		//{
		//	this.StartCoroutine(this.AddModuleToEva(data));
		//}

		public void HandleFlightPartAttach(GameEvents.HostTargetAction<Part, Part> hostTargetAction)
		{
			try
			{
				if(!FlightGlobals.ActiveVessel.isEVA)
				{
					return;
				}
				foreach(var module in hostTargetAction.target.GetComponentsInChildren<ModuleActiveStrut>())
				{
					if(module.IsTargetOnly)
					{
						module.UnlinkAllConnectedTargeters();
					}
					else
					{
						module.Unlink();
					}
				}
			}
			catch (NullReferenceException)
			{
				//thrown on launch, don't know why
			}
		}

		public void HandleFlightPartUndock(Part data)
		{
			Debug.Log("[IRAS] part undocked");
		}

		private IEnumerator HighlightMouseOverPart(Part mouseOverPart)
		{
			var lPart = GetMohdForPart(mouseOverPart);
			while(mouseOverPart != null && lPart != null && !lPart.Reset)
			{
				lPart.Part.SetHighlightColor(Color.blue);
				lPart.Part.SetHighlight(true, false);
				lPart.Reset = true;
				yield return new WaitForEndOfFrame();
				//yield return new WaitForSeconds(0.1f);
				lPart = GetMohdForPart(mouseOverPart);
			}
			RemoveMohdFromList(lPart);
			if(mouseOverPart != null)
			{
				mouseOverPart.SetHighlightDefault();
			}
		}

		private static bool IsQueueEmpty()
		{
			lock (idResetQueueLock)
			{
				return idResetQueue.Count == 0;
			}
		}

		public void OnDestroy()
		{
			GameEvents.onPartRemove.Remove(HandleEditorPartDetach);
			GameEvents.onPartUndock.Remove(HandleFlightPartUndock);
			GameEvents.onPartAttach.Remove(HandleFlightPartAttach);
			GameEvents.onPartAttach.Remove(HandleEditorPartAttach);
		}

		public static IEnumerator PlaceNewPart(Part hittedPart, RaycastHit hit)
		{
			var rayres = new RaycastResult {HittedPart = hittedPart, Hit = hit};
			return PlaceNewPart(rayres);
		}

		private static IEnumerator PlaceNewPart(RaycastResult raycast)
		{
			var activeVessel = FlightGlobals.ActiveVessel;
			NewSpawnedPart.transform.position = raycast.Hit.point;
			NewSpawnedPart.GetComponent<Rigidbody>().velocity = raycast.HittedPart.GetComponent<Rigidbody>().velocity;
			NewSpawnedPart.GetComponent<Rigidbody>().angularVelocity = raycast.HittedPart.GetComponent<Rigidbody>().angularVelocity;

			yield return new WaitForSeconds(0.1f);
			NewSpawnedPart.transform.rotation = raycast.HittedPart.transform.rotation;
			NewSpawnedPart.transform.position = raycast.Hit.point;

			NewSpawnedPart.transform.LookAt(CurrentTargeter.transform_().position);
			NewSpawnedPart.transform.rotation =
				Quaternion.FromToRotation(NewSpawnedPart.transform.up, raycast.Hit.normal)*
				NewSpawnedPart.transform.rotation;

			yield return new WaitForFixedUpdate();
			var targetModuleName = Config.Instance.ModuleActiveStrutFreeAttachTarget;
			if(!NewSpawnedPart.Modules.Contains(targetModuleName))
			{
				Debug.Log("[IRAS][ERR] spawned part contains no target module. Panic!!");
				NewSpawnedPart.decouple();
				Destroy(NewSpawnedPart);
			}
			var module = NewSpawnedPart.Modules[Config.Instance.ModuleActiveStrutFreeAttachTarget];
			NewSpawnedPart.transform.position = raycast.Hit.point;
			yield return new WaitForFixedUpdate();
			yield return new WaitForSeconds(0.1f);
			yield return new WaitForFixedUpdate();
		//	CurrentTargeter.PlaceFreeAttach(NewSpawnedPart);
			partPlacementInProgress = false;
			NewSpawnedPart = null;
		}

		private static void TrimQueue()
		{
			lock (idResetQueueLock)
			{
				idResetQueue.TrimExcess();
			}
		}

		private static void UpdateStraightOutHint(ModuleActiveStrut module, GameObject hint)
		{
			hint.SetActive(false);
			var rayres = Utilities.PerformRaycastIntoDir(module.Origin.position, module.RealModelForward,
				module.RealModelForward, module.part);
			var trans = hint.transform;
			trans.position = module.Origin.position;
			var dist = rayres.HitResult ? rayres.DistanceFromOrigin/2f : Config.Instance.MaxDistance;
			if(rayres.HitResult)
			{
				trans.LookAt(rayres.Hit.point);
			}
			else
			{
				trans.LookAt(module.Origin.transform.position + module.RealModelForward);
			}
			trans.Rotate(new Vector3(0, 1, 0), 90f);
			trans.Rotate(new Vector3(0, 0, 1), 90f);
			trans.localScale = new Vector3(0.05f, dist, 0.05f);
			trans.Translate(new Vector3(0f, dist, 0f));
			hint.SetActive(true);
		}

		private MouseOverHighlightData GetMohdForPart(Part mopart)
		{
			if(mopart == null)
			{
				return null;
			}
			lock (mouseOverSetLock)
			{
				return mouseOverPartsData.FirstOrDefault(mohd => mohd.Part == mopart);
			}
		}

		private void PointToMousePosition(Vector3 mp, RaycastResult rayRes)
		{
			var startPos = CurrentTargeter.Origin_().position;
			connector.SetActive(true);
			var trans = connector.transform;
			trans.position = startPos;
			trans.LookAt(mp);
			trans.localScale = new Vector3(trans.position.x, trans.position.y, 1);
			var dist = Vector3.Distance(Vector3.zero, trans.InverseTransformPoint(mp))/2.0f;
			trans.localScale = new Vector3(0.05f, dist, 0.05f);
			trans.Rotate(new Vector3(0, 0, 1), 90f);
			trans.Rotate(new Vector3(1, 0, 0), 90f);
			trans.Translate(new Vector3(0f, dist, 0f));
			//if(rayRes.HitResult)
			//{
			//	var mouseOverPart = rayRes.HittedPart;
			//	if(mouseOverPart != null)
			//	{
			//		if(!this._setMouseOverPart(mouseOverPart))
			//		{
			//			this.StartCoroutine(this.HighlightMouseOverPart(mouseOverPart));
			//		}
			//	}
			//}
		}

		private void ProcessFixedUpdate()
		{
			if(unusedTargetPartRemovalCounter > 0)
			{
				unusedTargetPartRemovalCounter--;
			}
			else
			{
				unusedTargetPartRemovalCounter = UNUSED_TARGET_PART_REMOVAL_COUNTER_INTERVAL;
			}
			if(!HighLogic.LoadedSceneIsFlight)
			{
				return;
			}
		}

		private void ProcessFreeAttachPlacement(RaycastResult raycast)
		{
//			raycast.Hit.point

// FEHLER, mal ein Test... ich baue ein Objekt auf, wo ich auf das Target treffe... mal sehen ob ich das später auch für normale Teils brauche...

			CurrentTargeter.SetFreeLink(raycast);


/* das da unten ist alles Schrott... jetzt setzen wir mal ein Element...
 * 
			if(NewSpawnedPart == null)
			{
				Mode = AddonMode.None;
				if(Mode == AddonMode.FreeAttach)
				{
					CurrentTargeter.RemoveLink();
				}
				Debug.Log("[IRAS][ERR] no target part ready - aborting FreeAttach");
				return;
			}
			if(partPlacementInProgress)
			{
				return;
			}
			partPlacementInProgress = true;
			StartCoroutine(PlaceNewPart(raycast));*/

		}

		private static void _processIdResets()
		{
			if(idResetCounter > 0)
			{
				idResetCounter--;
				return;
			}
			idResetCounter = Config.ID_RESET_CHECK_INTERVAL;
			var updateFlag = false;
			while(!IsQueueEmpty())
			{
				var module = Dequeue();
				if(module != null)
				{
					module.ResetId();
				}
				updateFlag = true;
			}
			if(updateFlag)
			{
				Debug.Log("[IRAS] IDs have been updated.");
			}
			if(idResetTrimFlag)
			{
				TrimQueue();
			}
			else
			{
				idResetTrimFlag = true;
			}
		}

		private void RemoveMohdFromList(MouseOverHighlightData mohd)
		{
			if(mohd == null)
			{
				return;
			}
			lock (mouseOverSetLock)
			{
				mouseOverPartsData.Remove(mohd);
			}
		}

		private bool SetMouseOverPart(Part mopart)
		{
			lock (mouseOverSetLock)
			{
				var lp = mouseOverPartsData.FirstOrDefault(mohd => mohd.Part == mopart);
				if(lp != null)
				{
					lp.Reset = false;
					return true;
				}
				mouseOverPartsData.Add(new MouseOverHighlightData(mopart));
				return false;
			}
		}

		private class MouseOverHighlightData
		{
			internal MouseOverHighlightData(Part part)
			{
				Part = part;
				Reset = false;
			}

			internal Part Part { get; private set; }
			internal bool Reset { get; set; }
		}
	}

	public enum AddonMode
	{
		FreeAttach,
		Link,
		None
	}

	internal struct LayerBackup
	{
		internal LayerBackup(int layer, Part part) : this()
		{
			Layer = layer;
			Part = part;
		}

		internal int Layer { get; private set; }
		internal Part Part { get; private set; }
	}
}
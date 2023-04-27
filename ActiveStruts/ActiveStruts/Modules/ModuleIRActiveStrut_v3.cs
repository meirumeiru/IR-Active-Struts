using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using ActiveStruts.Addons;
using ActiveStruts.Effects;
using ActiveStruts.Util;


namespace ActiveStruts.Modules
{
	public class ModuleIRActiveStrut_v3 : PartModule, IJointLockState, IActiveStrut // FEHLER, IActiveStrut ist temp während Umstellung
	{
// FEHLER, Würgi Würgi
public void UpdateGui() {}
public Transform Origin_() { return part.transform; } // das braucht der aussen um die grüne Linie zu zeichnen
		public Transform transform_() { return null; }
public Vector3 RealModelForward { get { return Vector3.zero; } }

public Part Part() { return part; }


		[KSPField(isPersistant = false)] public Vector3 axis = Vector3.left;	// rotation axis of the anchor
		[KSPField(isPersistant = false)] public Vector3 pointer = Vector3.up;	// rotation axis of the strut

		[KSPField(isPersistant = true)] private float breakForce = 10000f;
		[KSPField(isPersistant = true)] private float breakTorque = 10000f;

		[KSPField(isPersistant = true)] private float translationalForce = 10000f;
		[KSPField(isPersistant = true)] private float rotationalForce = 10000f;

		[KSPField(isPersistant = true)] private float translationalSpring = 100000f;
		[KSPField(isPersistant = true)] private float rotationalSpring = 100000f;

		[KSPField(isPersistant = false)] public float maxDistance = 7f; // FEHLER, viel zu gross
		[KSPField(isPersistant = false)] public float maxAngle = 95f;	// FEHLER, 95? wohin? gegen hinten gedreht? geht doch gar nicht... -> auch falsch... 80 oder so?
			// FEHLER, Werte nutzen !! die zwei da oben werden nie genutzt im Moment

		// Sound
		[KSPField(isPersistant = false)] public float soundPitch = 0.8f;
		[KSPField(isPersistant = false)] public float soundVolume = 0.5f;
		[KSPField(isPersistant = false)] public string soundFilePath = "MagicSmokeIndustries/Sounds/infernalRoboticMotor";
		protected SoundSource soundSound = null;

		public FXGroup SoundAttach;
		public FXGroup SoundBreak;
		public FXGroup SoundDetach;

		// Meshes
		public Transform Anchor;
		[KSPField(isPersistant = false)] public String AnchorName;
		public Quaternion AnchorOriginalRotation;

		public Transform LightsDull;
		[KSPField(isPersistant = false)] public string LightsDullName;
		private Vector3 LightsDullOriginalPosition;
		private Quaternion LightsDullOriginalRotation;

		public Transform LightsBright;
		[KSPField(isPersistant = false)] public string LightsBrightName;
		private Vector3 LightsBrightOriginalPosition;
		private Quaternion LightsBrightOriginalRotation;

		public Transform Strut;
		[KSPField(isPersistant = false)] public string StrutName;
		[KSPField(isPersistant = false)] public float StrutScaleFactor;
		private Vector3 StrutOriginalPosition;
		private Quaternion StrutOriginalRotation;

		public Transform Grappler;
		[KSPField(isPersistant = false)] public string GrapplerName;
		[KSPField(isPersistant = false)] public float GrapplerOffset;
		private Vector3 GrapplerOriginalPosition;
		private Quaternion GrapplerOriginalRotation;


		private bool isOnRails = false;


		ModuleIRActiveStrutTarget_v3 connectedPart = null;

		enum Mode { free, connecting, linked, retracting, broken };
		Mode currentMode = Mode.free;


		public ModuleIRActiveStrut_v3()
		{
			DebugInit();
		}

		////////////////////////////////////////
		// Callbacks

		public override void OnAwake()
		{
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);

			DebugInit();

			FindMeshes();

			if(soundSound == null)
				soundSound = new SoundSource(part, "motor");
			soundSound.Setup(soundFilePath, true);
/*
			if(SoundAttach == null || SoundBreak == null || SoundDetach == null ||
				!GameDatabase.Instance.ExistsAudioClip(Config.Instance.SoundAttachFileUrl) ||
				!GameDatabase.Instance.ExistsAudioClip(Config.Instance.SoundDetachFileUrl) ||
				!GameDatabase.Instance.ExistsAudioClip(Config.Instance.SoundBreakFileUrl))
			{
				Debug.Log("[IRAS] sounds cannot be loaded." +
						  (SoundAttach == null ? "FXGroup not instantiated" : "sound file not found"));
			}
*/
			SetupFxGroup(SoundAttach, gameObject, Config.Instance.SoundAttachFileUrl);
			SetupFxGroup(SoundDetach, gameObject, Config.Instance.SoundDetachFileUrl);
			SetupFxGroup(SoundBreak, gameObject, Config.Instance.SoundBreakFileUrl);

			UpdateUI();
		}

		public void OnDestroy()
		{
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
		}

		public override void OnLoad(ConfigNode config)
		{
			base.OnLoad(config);
		}

		public void OnVesselGoOnRails(Vessel v)
		{
			if(part.vessel == v)
			{
				isOnRails = true;
			}
		}

		public void OnVesselGoOffRails(Vessel v)
		{
			if(part.vessel == v)
			{
				isOnRails = false;
			}
		}

		public void OnJointBreak(float breakForce)
		{
			currentMode = Mode.broken;

			Strut.localScale = Vector3.zero;
			Grappler.localScale = Vector3.zero;
				// FEHLER, oder die zwei da oben zerstören???

			PlayBreakSound();

//			OSD.PostMessage("Joint broken!");
		}

		////////////////////////////////////////
		// Functions

		private void FindMeshes()
		{
			Anchor = part.FindModelTransform("Anchor");
			Anchor.localRotation *= Quaternion.AngleAxis(90f, Vector3.right);

			AnchorOriginalRotation = Anchor.localRotation;

			if(!string.IsNullOrEmpty(GrapplerName))
			{
				Grappler = part.FindModelTransform(GrapplerName);

				GrapplerOriginalPosition = Grappler.localPosition;
				GrapplerOriginalRotation = Grappler.localRotation;
			}

			if(!string.IsNullOrEmpty(StrutName))
			{
				Strut = part.FindModelTransform(StrutName);

				StrutOriginalPosition = Strut.localPosition;
				StrutOriginalRotation = Strut.localRotation;

				DestroyImmediate(Strut.GetComponent<Collider>());
			}

			if(!string.IsNullOrEmpty(LightsBrightName))
			{
				LightsBright = part.FindModelTransform(LightsBrightName);

				LightsBrightOriginalPosition = LightsBright.localPosition;
				LightsBrightOriginalRotation = LightsBright.localRotation;

				DestroyImmediate(LightsBright.GetComponent<Collider>());
			}

			if(!string.IsNullOrEmpty(LightsDullName))
			{
				LightsDull = part.FindModelTransform(LightsDullName);

				LightsDullOriginalPosition = LightsDull.localPosition;
				LightsDullOriginalRotation = LightsDull.localRotation;

				DestroyImmediate(LightsDull.GetComponent<Collider>());
			}
/*
			if(!string.IsNullOrEmpty(HeadName))
			{
				var head = part.FindModelTransform(HeadName);
				StrutOrigin = head.transform;
				headTransform = head;
				ModelFeatures.Add(ModelFeaturesType.HeadExtension, true);
			}
			else
				ModelFeatures.Add(ModelFeaturesType.HeadExtension, false);

			if(!string.IsNullOrEmpty(SimpleLightsName))
			{
				simpleLights = part.FindModelTransform(SimpleLightsName);
				simpleLightsSecondary = part.FindModelTransform(SimpleLightsSecondaryName);
				featureOrientation.Add(ModelFeaturesType.SimpleLights, new OrientationInfo(SimpleLightsForward));
				ModelFeatures.Add(ModelFeaturesType.SimpleLights, true);
			}
			else
				ModelFeatures.Add(ModelFeaturesType.SimpleLights, false);
*/
		}

		public void SetLink(ModuleIRActiveStrutTarget_v3 target)
		{
			connectedPart = target;
			target.connectedPart = this;

			currentMode = Mode.connecting;

			CreateTarget(target.part, target.transform.position, -target.transform.right);

			UpdateUI();
		}

		public void SetFreeLink(RaycastResult raycast)
		{
			connectedPart = raycast.HittedPart.GetComponent<ModuleIRActiveStrutTarget_v3>();

			currentMode = Mode.connecting;

			CreateTarget(raycast.HittedPart, raycast.Hit.point, raycast.Hit.normal);

			UpdateUI();
		}

		public void RemoveLink()
		{
			if(connectedPart != null)
			{
				ModuleIRActiveStrut_v3 cP = connectedPart.GetComponent<ModuleIRActiveStrut_v3>();

				if(cP)
					cP.connectedPart = null;
				connectedPart = null;

				DestroyJoints();

				DestroyTarget();

				currentMode = Mode.retracting;
			}

			UpdateUI();
		}

			// FEHLER, siehe IR -> gibt's neu von Unity direkt
		public static float AngleSigned(Vector3 v1, Vector3 v2, Vector3 n)
		{
			// negativ, because unity is left handed and we would have to correct this always

			return -Mathf.Atan2(
				Vector3.Dot(n.normalized, Vector3.Cross(v1.normalized, v2.normalized)),
				Vector3.Dot(v1.normalized, v2.normalized)) * Mathf.Rad2Deg;
		}

		private float MinimizeTurn(float angle)
		{
			while(angle > 90)
				angle -= 180;

			while(angle < -90)
				angle += 180;

			return angle;
		}

// FEHLER, sowas von Hilfsfunktion
		private Vector3 vect(Transform trf, int idx)
		{
			switch(idx)
			{
			case 0:
				return trf.up;
			case 1:
				return -trf.up;
			case 2:
				return trf.right;
			case 3:
				return -trf.right;
			case 4:
				return trf.forward;
			case 5:
				return -trf.forward;
			}

			return Vector3.zero;
		}

// FEHLER, sowas von Hilfsfunktion
		private Quaternion quat(Transform trf, int idx)
		{
			return Quaternion.LookRotation(vect(trf, (idx % 36) / 6), vect(trf, idx % 6));
		}

		// FEHLER, axis vom Ziel berücksichtigen... echt jetzt
		private int CalculateBestHeadTurn()
		{
			int[] id = { 4, 11, 25, 30 };
			float[] fs = { 0, 0, 0, 0 };

			for(int i = 0; i < 4; i++)
			{
				Quaternion q = quat(targetObject.transform, id[i]);
				q = q * Quaternion.Inverse(Grappler.rotation); // sonst wär's nicht relativ
				fs[i] = Vector3.Angle(Grappler.forward, q * Grappler.forward);
			}

			int sel = 0;
			for(int i = 1; i < 4; i++)
				if(fs[i] < fs[sel])
					sel = i;

			return id[sel];
		}
/*
		private void ResetMeshPositions()
		{
			Anchor.localRotation = AnchorOriginalRotation;

			LightsDull.localRotation = LightsDullOriginalRotation;
			LightsDull.localPosition = LightsDullOriginalPosition;
			LightsBright.localRotation = LightsBrightOriginalRotation;
			LightsBright.localPosition = LightsBrightOriginalPosition;
			Strut.localRotation = StrutOriginalRotation;
			Strut.localPosition = StrutOriginalPosition;
			Strut.localScale = Vector3.one;
			Grappler.localRotation = GrapplerOriginalRotation;
			Grappler.localPosition = GrapplerOriginalPosition;
		}

		private void UpdateMeshPositionsInOne(float angleBase, float angleStrut)
		{
			Quaternion rot = Quaternion.AngleAxis(angleBase, axis);

			Anchor.localRotation = rot * AnchorOriginalRotation;

			rot = rot * Quaternion.AngleAxis(angleStrut, pointer);

			LightsDull.localRotation = rot * LightsDullOriginalRotation;
			LightsBright.localRotation = rot * LightsBrightOriginalRotation;
			Strut.localRotation = rot * StrutOriginalRotation;
			Grappler.localRotation = rot * GrapplerOriginalRotation;
		}
*/
		ConfigurableJoint sourceJoint = null;

		GameObject targetObject = null;
		ConfigurableJoint targetObjectJoint = null;

		private void CreateTarget(Part partTarget, Vector3 positionTarget, Vector3 normalTarget)
		{
			DestroyTarget();

// build object (at target head position)

			targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			if(targetObject.GetComponent<Rigidbody>() == null)
				targetObject.AddComponent<Rigidbody>();
		//	targetObject.name = "targetObject";
			DestroyImmediate(targetObject.GetComponent<Collider>()); // von mir aus...
			const float LOCAL_ANCHOR_DIM = 0.05f;
			targetObject.transform.localScale = new Vector3(LOCAL_ANCHOR_DIM, LOCAL_ANCHOR_DIM, LOCAL_ANCHOR_DIM);
			var mr = targetObject.GetComponent<MeshRenderer>();
			mr.name = targetObject.name;
			mr.material = new Material(Shader.Find("Diffuse")) {color = Color.magenta};
mr.enabled = false; // FEHLER, später den Mist ganz raus
			targetObject.GetComponent<Rigidbody>().mass = 0.01f;
			targetObject.SetActive(true);

			if(Vector3.Dot(normalTarget, Vector3.right) < Vector3.Dot(normalTarget, Vector3.up))
				targetObject.transform.rotation = Quaternion.LookRotation(Vector3.Cross(normalTarget, Vector3.up),
					Quaternion.AngleAxis(90, normalTarget) * Vector3.Cross(normalTarget, Vector3.up));
			else
				targetObject.transform.rotation = Quaternion.LookRotation(Vector3.Cross(normalTarget, Vector3.right),
					Quaternion.AngleAxis(90, normalTarget) * Vector3.Cross(normalTarget, Vector3.right));

			targetObject.transform.position = positionTarget;

// joint hinterher schiessen

			// build joint in new object and connect it to the target

			targetObjectJoint = partTarget.gameObject.AddComponent<ConfigurableJoint>();
				
			targetObjectJoint.connectedBody = targetObject.GetComponent<Rigidbody>();

			Vector3 v1 = part.transform.position - targetObject.transform.position; // x-axis -> must point along the strut
			Vector3 v2 = Vector3.up; Vector3 v3 = Vector3.right;

			Vector3.OrthoNormalize(ref v1, ref v2, ref v3);

			targetObjectJoint.axis = v1; targetObjectJoint.secondaryAxis = v2;
// FEHLER, diese Achse... funktioniert das so oder müsste ich die ab und zu nachrichten?? oder richtet die sich selber aus?... nein, tut sie nicht -> beim Zieh-Modus die nachrichten...

			targetObjectJoint.breakForce = breakForce;
			targetObjectJoint.breakTorque = breakTorque;

			targetObjectJoint.xMotion = ConfigurableJointMotion.Free;
			targetObjectJoint.yMotion = ConfigurableJointMotion.Free;
			targetObjectJoint.zMotion = ConfigurableJointMotion.Free;
			targetObjectJoint.angularXMotion = ConfigurableJointMotion.Free;
			targetObjectJoint.angularYMotion = ConfigurableJointMotion.Free;
			targetObjectJoint.angularZMotion = ConfigurableJointMotion.Free;

			targetObjectJoint.rotationDriveMode = RotationDriveMode.XYAndZ;

			targetObjectJoint.angularXDrive = targetObjectJoint.angularYZDrive = new JointDrive
			{ positionSpring = rotationalSpring, positionDamper = 0f, maximumForce = rotationalForce };

			// only rotation on this side (grappler)
			targetObjectJoint.xMotion = targetObjectJoint.yMotion = targetObjectJoint.zMotion = ConfigurableJointMotion.Locked;

			// allow the strut to pull back or act as spring
			targetObjectJoint.xMotion = ConfigurableJointMotion.Free;
			targetObjectJoint.xDrive = new JointDrive	
			{ positionSpring = translationalSpring, positionDamper = 0f, maximumForce = translationalForce };

			targetObjectJoint.projectionMode = JointProjectionMode.None;
		//	targetObjectJoint.projectionAngle = 0f;
		//	targetObjectJoint.projectionDistance = 0f;

			// pull... but we don't want this now
//	targetObjectJoint.targetPosition = Vector3.right // move always along x axis!!
//		* (part.transform.position - targetObject.transform.position).magnitude; // / 2;

			// just keep the position
			targetObjectJoint.targetPosition = Vector3.zero;
		}

		private void CreateJoints()
		{
			if(currentMode != Mode.linked)
				return;

			DestroyJoints();

			// build joint and connect it to the new object

			sourceJoint = part.GetComponent<Rigidbody>().gameObject.AddComponent<ConfigurableJoint>();

			sourceJoint.connectedBody = targetObject.GetComponent<Rigidbody>();

			sourceJoint.breakForce = breakForce;
			sourceJoint.breakTorque = breakTorque;

			sourceJoint.xMotion = ConfigurableJointMotion.Free;
			sourceJoint.yMotion = ConfigurableJointMotion.Free;
			sourceJoint.zMotion = ConfigurableJointMotion.Free;
			sourceJoint.angularXMotion = ConfigurableJointMotion.Free;
			sourceJoint.angularYMotion = ConfigurableJointMotion.Free;
			sourceJoint.angularZMotion = ConfigurableJointMotion.Free;

			sourceJoint.rotationDriveMode = RotationDriveMode.XYAndZ;

			sourceJoint.angularXDrive = sourceJoint.angularYZDrive = new JointDrive
			{ positionSpring = rotationalSpring, positionDamper = 0f, maximumForce = rotationalForce };

			// only rotation on this side (base)
			sourceJoint.xMotion = sourceJoint.yMotion = sourceJoint.zMotion = ConfigurableJointMotion.Locked;

			sourceJoint.projectionMode = JointProjectionMode.None;
		//	sourceJoint.projectionAngle = 0f;
		//	sourceJoint.projectionDistance = 0f;


			PlayAttachSound();
		}

		private void DestroyJoints()
		{
			if(sourceJoint)
				Destroy(sourceJoint);
			sourceJoint = null;

			PlayDetachSound();
		}

		private void DestroyTarget()
		{
			if(targetObjectJoint)
				Destroy(targetObjectJoint);
			targetObjectJoint = null;

			if(targetObject)
				Destroy(targetObject);
			targetObject = null;
		}

		private static void SetupFxGroup(FXGroup group, GameObject gameObject, string audioFileUrl)
		{
			group.audio = gameObject.AddComponent<AudioSource>();
			group.audio.clip = GameDatabase.Instance.GetAudioClip(audioFileUrl);
			group.audio.dopplerLevel = 0f;
			group.audio.rolloffMode = AudioRolloffMode.Linear;
			group.audio.maxDistance = 30f;
			group.audio.loop = false;
			group.audio.playOnAwake = false;
			group.audio.volume = GameSettings.SHIP_VOLUME;
		}

		private void PlayAudio(FXGroup group)
		{
			if(group == null || group.audio == null)
				return;
			group.audio.Play();
		}

		public void PlayAttachSound()
		{
			PlayAudio(SoundAttach);
		}

		public void PlayBreakSound()
		{
			PlayAudio(SoundBreak);
		}

		public void PlayDetachSound()
		{
			PlayAudio(SoundDetach);
		}

		////////////////////////////////////////
		// Update-Functions

		public void FixedUpdate()
		{
//DrawRelative(0, part.transform.position, part.transform.TransformDirection(Vector3.up));
//DrawRelative(1, part.transform.position, part.transform.TransformDirection(Vector3.right));
//DrawRelative(2, part.transform.position, part.transform.TransformDirection(Vector3.forward));
		}

//		public void Update()
//		{
//		}

// FEHLER FEHLER; temp
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE, guiName = "Test")]
		public void Test()
		{
			Quaternion rotationHead = quat(targetObject.transform, CalculateBestHeadTurn());
			Grappler.rotation = rotationHead;
		}

		public void LateUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight)
				return;

			switch(currentMode)
			{
			case Mode.free:
			case Mode.broken:
				break;

			case Mode.connecting:
			case Mode.linked:
				{
					// zuerst die Rotation der Basis

					Vector3 globalAxis = part.transform.TransformDirection(axis);

					float angleBase =
						MinimizeTurn(
							90 + AngleSigned(
								part.transform.TransformDirection(pointer),
								Vector3.ProjectOnPlane(targetObject.transform.position - part.transform.position, globalAxis), globalAxis));

					Quaternion rotationBase = Quaternion.AngleAxis(-angleBase, axis);

					// dann die Rotation der Strebe

					Vector3 globalPointer = part.transform.TransformDirection(rotationBase * pointer);

					float angleStrut =
						AngleSigned(
							globalAxis,
							targetObject.transform.position - part.transform.position, globalPointer);	// FEHLER, ausser er hätte einen Offset, dann statt part-position diesen noch nutzen

					Quaternion rotationStrut = Quaternion.AngleAxis(-angleStrut, rotationBase * pointer);

					// jetzt noch die Länge der Strebe

					float distance = (targetObject.transform.position - part.transform.position).magnitude;

					// und die Drehung des Kopfs

					Quaternion rotationHead = quat(targetObject.transform, CalculateBestHeadTurn());
						// die wär global und von allem oberen losgelöst... offenbar


					if(currentMode == Mode.linked)
					{
						Quaternion rot = Quaternion.AngleAxis(-angleBase, axis);

						Anchor.localRotation = rot * AnchorOriginalRotation;

						rot = rot * Quaternion.AngleAxis(-angleStrut, pointer);

						LightsDull.localRotation = rot * LightsDullOriginalRotation;
						LightsBright.localRotation = rot * LightsBrightOriginalRotation;
						Strut.localRotation = rot * StrutOriginalRotation;

						Strut.localScale = new Vector3(distance * StrutScaleFactor, 1, 1);

						Grappler.rotation = rotationHead;
						Grappler.position = targetObject.transform.position;
					}
					else // Schrittweise sich annähern versuchen
					{
						Quaternion rot1 = Quaternion.AngleAxis(-angleBase, axis);

						Quaternion AnchorRotation = rot1 * AnchorOriginalRotation;

						float angle = Quaternion.Angle(Anchor.localRotation, AnchorRotation);

						if(angle >= 1f)
						{
							Quaternion lrot1 = Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(Anchor.localRotation) * AnchorRotation, 1f / angle);

							Anchor.localRotation *= lrot1;

							LightsDull.localRotation *= lrot1;
							LightsBright.localRotation *= lrot1;
							Strut.localRotation *= lrot1;
							Grappler.localRotation *= lrot1;

							soundSound.Update(soundVolume, 7 * soundPitch);
							soundSound.Play();
						}
						else
						{
							Quaternion lrot1 = Quaternion.Inverse(Anchor.localRotation) * AnchorRotation;
					
							Anchor.localRotation *= lrot1; // FEHLER, oder hier = AnchorRotation machen?

							LightsDull.localRotation *= lrot1;
							LightsBright.localRotation *= lrot1;
							Strut.localRotation *= lrot1;
							Grappler.localRotation *= lrot1;


							Quaternion rot2 = Quaternion.AngleAxis(-angleStrut, pointer);

							Quaternion StrutRotation = rot1 * rot2 * StrutOriginalRotation;

							angle = Quaternion.Angle(Strut.localRotation, StrutRotation);

							if(angle >= 1f)
							{
								Quaternion lrot2 = Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(Strut.localRotation) * StrutRotation, 1f / angle);

								LightsDull.localRotation *= lrot2;
								LightsBright.localRotation *= lrot2;
								Strut.localRotation *= lrot2;
								Grappler.localRotation *= lrot2;

								Grappler.localPosition = Strut.localRotation * GrapplerOriginalPosition;

								soundSound.Update(soundVolume, 4 * soundPitch);
								soundSound.Play();
							}
							else
							{
								LightsDull.localRotation = rot1 * rot2 * LightsDullOriginalRotation;
								LightsBright.localRotation = rot1 * rot2 * LightsBrightOriginalRotation;
								Strut.localRotation = StrutRotation;
								Grappler.localRotation = rot1 * rot2 * GrapplerOriginalRotation;

								Grappler.localPosition = Strut.localRotation * GrapplerOriginalPosition;

								if(distance - (Strut.localScale.x / StrutScaleFactor) > 0.005f)
								{
									float d = (Strut.localScale.x / StrutScaleFactor) + 0.005f;

									Strut.localScale = new Vector3(d * StrutScaleFactor, 1, 1);

									Grappler.position = part.transform.position + ((targetObject.transform.position - part.transform.position).normalized * d);

									if(distance - d < 0.5f)
										Grappler.rotation = Quaternion.Slerp(Grappler.rotation, rotationHead, (distance < 0.5f) ? (d / distance) : ((0.5f - distance + d) / 0.5f));

									soundSound.Update(soundVolume * 3, soundPitch);
									soundSound.Play();
								}
								else
								{
									Strut.localScale = new Vector3(distance * StrutScaleFactor, 1, 1);

									Grappler.rotation = rotationHead;
									Grappler.position = targetObject.transform.position;

									currentMode = Mode.linked;
									soundSound.Stop();

									CreateJoints();
								}
							}
						}
					}
				}
				break;

			case Mode.retracting:
				{
					// zuerst die Rotation der Basis

					float angleBase =
						MinimizeTurn(AngleSigned(AnchorOriginalRotation * pointer, Anchor.localRotation * pointer, axis));

					Quaternion rotationBase = Quaternion.AngleAxis(-angleBase, axis);

					// dann die Rotation der Strebe

					float angleStrut =
						AngleSigned(StrutOriginalRotation * axis, Strut.localRotation * axis, rotationBase * pointer);

					Quaternion rotationStrut = Quaternion.AngleAxis(-angleStrut, rotationBase * pointer);

					// jetzt noch die Länge der Strebe

					float distance = (Grappler.position - part.transform.position).magnitude;

					// zuerst zurückziehen und den Kopf zurückdrehen

					if(Strut.localScale.x >= (1 + 0.005 * StrutScaleFactor))
					{
						Grappler.localPosition = Strut.localRotation * GrapplerOriginalPosition;

						float d = (Strut.localScale.x / StrutScaleFactor) - 0.005f;

						Strut.localScale = new Vector3(d * StrutScaleFactor, 1, 1);

						Grappler.position = part.transform.position + ((Grappler.position - part.transform.position).normalized * d);

						Quaternion rot = Quaternion.AngleAxis(-angleBase, axis) * Quaternion.AngleAxis(-angleStrut, pointer);

						Quaternion rotationHead = rot * GrapplerOriginalRotation;

						float angle = Quaternion.Angle(Grappler.rotation, rotationHead);

						if(angle > 3f)
							Grappler.localRotation = Quaternion.Slerp(Grappler.localRotation, rotationHead, 3f / angle);
						else
							Grappler.localRotation = rotationHead;

						soundSound.Update(soundVolume * 3, soundPitch);
						soundSound.Play();
					}
					else
					{
						Strut.localScale = Vector3.one;

						if(Mathf.Abs(angleStrut) > 1f)
						{
							angleStrut += (angleStrut < 0f ? 1f : -1f);

							Quaternion rot = Quaternion.AngleAxis(-angleBase, axis);

							Anchor.localRotation = rot * AnchorOriginalRotation;

							rot = rot * Quaternion.AngleAxis(-angleStrut, pointer);

							LightsDull.localRotation = rot * LightsDullOriginalRotation;
							LightsBright.localRotation = rot * LightsBrightOriginalRotation;
							Strut.localRotation = rot * StrutOriginalRotation;
							Grappler.localRotation = rot * GrapplerOriginalRotation;

							Grappler.localPosition = Strut.localRotation * GrapplerOriginalPosition;

							soundSound.Update(soundVolume, 4 * soundPitch);
							soundSound.Play();
						}
						else
						{
							if(Mathf.Abs(angleBase) >= 1f)
							{
								angleBase += (angleBase < 0f ? 1f : -1f);

								Quaternion rot = Quaternion.AngleAxis(-angleBase, axis);

								Anchor.localRotation = rot * AnchorOriginalRotation;

								LightsDull.localRotation = rot * LightsDullOriginalRotation;
								LightsBright.localRotation = rot * LightsBrightOriginalRotation;
								Strut.localRotation = rot * StrutOriginalRotation;
								Grappler.localRotation = rot * GrapplerOriginalRotation;

								Grappler.localPosition = Strut.localRotation * GrapplerOriginalPosition;

								soundSound.Update(soundVolume, 7 * soundPitch);
								soundSound.Play();
							}
							else
							{
								Anchor.localRotation = AnchorOriginalRotation;

								LightsDull.localRotation = LightsDullOriginalRotation;
								LightsBright.localRotation = LightsBrightOriginalRotation;
								Strut.localRotation = StrutOriginalRotation;
								Grappler.localRotation = GrapplerOriginalRotation;
			
								Grappler.localPosition = GrapplerOriginalPosition;

								currentMode = Mode.free;
								soundSound.Stop();
							}
						}
					}
				}
				break;
			}
		}

		////////////////////////////////////////
		// IJointLockState (auto strut support)

		bool IJointLockState.IsJointUnlocked()
		{
			return true;
		}

		////////////////////////////////////////
		// Context Menu

		private void UpdateUI()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				Events["Link"].active = Events["Link"].guiActive = (connectedPart == null);
				Events["Unlink"].active = Events["Unlink"].guiActive = (connectedPart != null);
			}
			else if(HighLogic.LoadedSceneIsEditor)
			{
				Events["Link"].active = Events["Link"].guiActiveEditor = (connectedPart == null);
				Events["Unlink"].active = Events["Unlink"].guiActiveEditor = (connectedPart != null);
			}
		}

		////////////////////////////////////////
		// Actions

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE, guiName = "Link")]
		public void Link()
		{
			if(ActiveStrutsAddon.CurrentTargeter != null)
				ActiveStrutsAddon.AbortLink();

			ActiveStrutsAddon.StartLink(this);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE, guiName = "Unlink")]
		public void Unlink()
		{
			RemoveLink();
			// FEHLER, bin ich gerade im Link-Modus, ist die Animation etwas falsch... (Grappler-Kopf und das Zurückdrehen des Struts)
		}

		////////////////////////////////////////
		// Debug
	
		private LineDrawer[] al = new LineDrawer[13];
		private Color[] alColor = new Color[13];

		private void DebugInit()
		{
			for(int i = 0; i < 13; i++)
				al[i] = new LineDrawer();

			alColor[0] = Color.red;
			alColor[1] = Color.green;
			alColor[2] = Color.yellow;
			alColor[3] = Color.magenta;	// axis
			alColor[4] = Color.blue;		// secondaryAxis
			alColor[5] = Color.white;
			alColor[6] = new Color(33.0f / 255.0f, 154.0f / 255.0f, 193.0f / 255.0f);
			alColor[7] = new Color(154.0f / 255.0f, 193.0f / 255.0f, 33.0f / 255.0f);
			alColor[8] = new Color(193.0f / 255.0f, 33.0f / 255.0f, 154.0f / 255.0f);
			alColor[9] = new Color(193.0f / 255.0f, 33.0f / 255.0f, 255.0f / 255.0f);
			alColor[10] = new Color(244.0f / 255.0f, 238.0f / 255.0f, 66.0f / 255.0f);
	//		alColor[11] = new Color(209.0f / 255.0f, 247.0f / 255.0f, 74.0f / 255.0f);
			alColor[11] = new Color(244.0f / 255.0f, 170.0f / 255.0f, 66.0f / 255.0f); // orange
			alColor[12] = new Color(247.0f / 255.0f, 186.0f / 255.0f, 74.0f / 255.0f);
		}

		private void DrawPointer(int idx, Vector3 p_vector)
		{
			al[idx].DrawLineInGameView(Vector3.zero, p_vector, alColor[idx]);
		}

		private void DrawRelative(int idx, Vector3 p_from, Vector3 p_vector)
		{
			al[idx].DrawLineInGameView(p_from, p_from + p_vector, alColor[idx]);
		}


/*

public bool bArsch = false;

public override void OnAwake()
{
	bArsch = true;
}

		private const ControlTypes EDITOR_LOCK_MASK = ControlTypes.EDITOR_PAD_PICK_PLACE | ControlTypes.EDITOR_ICON_PICK;
		private const float NORMAL_ANI_SPEED = 1.5f;
		private const float FAST_ANI_SPEED = 1000f;
		private readonly object freeAttachStrutUpdateLock = new object();

		[KSPField(isPersistant = true)] private bool AniExtended;
		[KSPField(isPersistant = false)] public string AnimationName;

		[KSPField(isPersistant = true)] public uint DockingVesselId;
		[KSPField(isPersistant = true)] public string DockingVesselName;
		[KSPField(isPersistant = true)] public string DockingVesselTypeString;

		[KSPField(isPersistant = true)] public string FreeAttachPositionOffsetVector;
		[KSPField(isPersistant = true)] public bool FreeAttachPositionOffsetVectorSetInEditor = false;
		[KSPField(isPersistant = true)] public string FreeAttachTargetId = Guid.Empty.ToString();



		[KSPField(isPersistant = false)] public float LightsOffset; // FEHLER, wozu?
	
		[KSPField(isPersistant = false)] public string HeadName;

		internal Transform StrutOrigin; // FEHLER, wozu??


		// all das Zeug hier ist lokal gemeint -> und da das Part nicht dreht, sollte das eigentlich immer gehen
Quaternion _rot; // lokale Rotation des Ankers... mal ein Versuch...
Quaternion _rot2; // lokale Rotation des Greifers (angewendet nach der des Ankers, soll _position ersetzen... vielleicht... oder ergänzen)

		[KSPField(isPersistant = true)] private Vector3 _position = Vector3.zero; // dahin zeige ich im Moment... das später speichern und so Zeugs...


		[KSPField(isPersistant = true)] public string Id = Guid.Empty.ToString();
		[KSPField(isPersistant = true)] public bool IdResetDone = false;

		[KSPField(isPersistant = true)] public bool IsConnectionOrigin = false;
		[KSPField(isPersistant = true)] public bool IsDocked;
		[KSPField(guiActive = true, guiName = "Enforced")] public bool IsEnforced = false;

		[KSPField(isPersistant = true)] public bool IsFreeAttached = false;
		[KSPField(isPersistant = true)] public bool IsHalfWayExtended = false;
		[KSPField(isPersistant = true)] public bool IsLinked = false;
		[KSPField(isPersistant = true)] public bool IsOwnVesselConnected = false;

		internal Dictionary<ModelFeaturesType, bool> ModelFeatures;
		public ModuleActiveStrut OldTargeter;
		public Transform Origin;
		[KSPField(isPersistant = false)] public string SimpleLightsForward = "FORWARD,false";
		[KSPField(isPersistant = false)] public string SimpleLightsName;
		[KSPField(isPersistant = false)] public string SimpleLightsSecondaryName;

		[KSPField(isPersistant = false, guiActive = true)] public string State = "n.a.";
		[KSPField(isPersistant = true)] public bool StraightOutAttachAppliedInEditor = false;
		[KSPField(guiActive = true)] public string Strength = LinkType.None.ToString();

		[KSPField(isPersistant = true)] public string TargetId = Guid.Empty.ToString();
		[KSPField(isPersistant = true)] public string TargeterId = Guid.Empty.ToString();
		private bool brightLightsExtended;
		private bool delayedStartFlag;
		private bool dullLightsExtended = true;
		private Dictionary<ModelFeaturesType, OrientationInfo> featureOrientation;
		private Part freeAttachPart;
		private Transform headTransform;
		private bool initialized;
		private ConfigurableJoint joint_;
		private ConfigurableJoint joint2_;

		private bool jointBroken;
		private LinkType linkType;
		private Mode mode = Mode.Undefined;
		private Vector3 oldTargetPosition = Vector3.zero;

		private Transform simpleLights;
		private Transform simpleLightsSecondary;
		private GameObject simpleStrut;
		private bool soundFlag;
		private bool straightOutAttached;
		private bool strutFinallyCreated;
		private int strutRealignCounter;
		private bool targetGrapplerVisible;
		private int ticksForDelayedStart;

		public Animation DeployAnimation
		{
			get { return string.IsNullOrEmpty(AnimationName) ? null : part.FindModelAnimators(AnimationName)[0]; }
		}

		private Part FreeAttachPart
		{
			get
			{
				if(freeAttachPart != null)
					return freeAttachPart;
				return freeAttachPart;
			}
		}

		public Vector3 FreeAttachPositionOffset
		{
			get
			{
				if(FreeAttachPositionOffsetVector == null)
					return Vector3.zero;
				var vArr = FreeAttachPositionOffsetVector.Split(' ').Select(Convert.ToSingle).ToArray();
				return new Vector3(vArr[0], vArr[1], vArr[2]);
			}
			set { FreeAttachPositionOffsetVector = String.Format("{0} {1} {2}", value.x, value.y, value.z); }
		}

		public Guid ID
		{
			get
			{
				if(Id == null || new Guid(Id) == Guid.Empty)
					Id = Guid.NewGuid().ToString();
				return new Guid(Id);
			}
		}

		private bool IsAnimationPlaying
		{
			get { return DeployAnimation != null && DeployAnimation.IsPlaying(AnimationName); }
		}

		public bool IsConnectionFree
		{
			get { return IsTargetOnly || !IsLinked || (IsLinked && Mode == Mode.Unlinked); }
		}

		public LinkType LinkType
		{
			get { return linkType; }
			set
			{
				linkType = value;
				Strength = value.ToString();
			}
		}

		public Mode Mode
		{
			get { return mode; }
			set
			{
				mode = value;
				State = value.ToString();
			}
		}

		public Vector3 RealModelForward
		{
			get { return -Origin.right; }
		}

		public ModuleActiveStrut Target
		{
			get { return TargetId == Guid.Empty.ToString() ? null : Utilities.GetStrutById(new Guid(TargetId)); }
			set { TargetId = value != null ? value.ID.ToString() : Guid.Empty.ToString(); }
		}

		public ModuleActiveStrut Targeter
		{
			get { return TargeterId == Guid.Empty.ToString() ? null : Utilities.GetStrutById(new Guid(TargeterId)); }
			set { TargeterId = value != null ? value.ID.ToString() : Guid.Empty.ToString(); }
		}

		public void ResetId()
		{
			var oldId = Id;
			Id = Guid.NewGuid().ToString();
			foreach(var moduleActiveStrut in Utilities.GetAllActiveStruts())
			{
				if(moduleActiveStrut.TargetId != null && moduleActiveStrut.TargetId == oldId)
					moduleActiveStrut.TargetId = Id;
				if(moduleActiveStrut.TargeterId != null && moduleActiveStrut.TargeterId == oldId)
					moduleActiveStrut.TargeterId = Id;
			}
			IdResetDone = true;
		}

		[KSPEvent(name = "AbortLink", active = false, guiName = "Abort Link", guiActiveEditor = true,
			guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
		public void AbortLink()
		{
			Mode = Mode.Unlinked;
			Utilities.ResetAllFromTargeting();
			ActiveStrutsAddon.Mode = AddonMode.None;
			ActiveStrutsAddon.FlexibleAttachActive = false;
			if(HighLogic.LoadedSceneIsEditor)
				InputLockManager.RemoveControlLock(Config.Instance.EditorInputLockId);
			OSD.PostMessage("Link aborted.");
			UpdateGui();
			RetractHead(NORMAL_ANI_SPEED);
		}


		public void CreateJoint(Rigidbody originBody, Rigidbody targetBody, LinkType type, Vector3 anchorPosition)
		{
			if(HighLogic.LoadedSceneIsFlight && part != null && part.attachJoint != null &&
				part.attachJoint.Joint != null)
			{
				part.attachJoint.Joint.breakForce = Mathf.Infinity;
				part.attachJoint.Joint.breakTorque = Mathf.Infinity;
				if(!IsFreeAttached && Target != null && Target.part != null && Target.part.attachJoint != null &&
					Target.part.attachJoint.Joint != null)
				{
					Target.part.attachJoint.Joint.breakForce = Mathf.Infinity;
					Target.part.attachJoint.Joint.breakTorque = Mathf.Infinity;
				}
			}

			LinkType = type;
			var breakForce = type.GetJointStrength();

			if(!IsFreeAttached)
			{
				var moduleActiveStrut = Target;
				if(moduleActiveStrut != null)
				{
					moduleActiveStrut.LinkType = type;
					IsOwnVesselConnected = moduleActiveStrut.vessel == vessel;
				}
			}
			else
				IsOwnVesselConnected = FreeAttachPart.vessel == vessel;


dd
			//var localAnchor = GameObject.CreatePrimitive(PrimitiveType.Cube);
			//if(localAnchor.GetComponent<Rigidbody>() == null)
			//    localAnchor.AddComponent<Rigidbody>();
			//localAnchor.name = name;
			//Object.DestroyImmediate(localAnchor.GetComponent<Collider>());
			//const float LOCAL_ANCHOR_DIM = 0.000001f;
			//localAnchor.transform.localScale = new Vector3(LOCAL_ANCHOR_DIM, LOCAL_ANCHOR_DIM, LOCAL_ANCHOR_DIM);
			//var mr = localAnchor.GetComponent<MeshRenderer>();
			//mr.name = name;
			//mr.material = new Material(Shader.Find("Diffuse")) {color = Color.magenta};
			//localAnchor.GetComponent<Rigidbody>().mass = 0.00001f;
			//localAnchor.SetActive(active);
			//return localAnchor;
dd
  
	//		if(!IsEnforced) -> FEHLER, bei non-Enforced müssen wir ihn einfach weniger stark machen... wir bauen jetzt was total neues aus diesem Zeugs hier... mal sehen was es wird
			{
				joint_ = originBody.gameObject.AddComponent<ConfigurableJoint>();
				joint_.connectedBody = targetBody;
				joint_.breakForce = joint_.breakTorque = Mathf.Infinity;
	//			joint_.xMotion = ConfigurableJointMotion.Locked;
	//			joint_.yMotion = ConfigurableJointMotion.Locked;
	//			joint_.zMotion = ConfigurableJointMotion.Locked;
	//			joint_.angularXMotion = ConfigurableJointMotion.Locked;
	//			joint_.angularYMotion = ConfigurableJointMotion.Locked;
	//			joint_.angularZMotion = ConfigurableJointMotion.Locked;

//				joint_.xMotion = ConfigurableJointMotion.Free;
//				joint_.yMotion = ConfigurableJointMotion.Free;
//				joint_.zMotion = ConfigurableJointMotion.Free;
				joint_.angularXMotion = ConfigurableJointMotion.Free;
				joint_.angularYMotion = ConfigurableJointMotion.Free;
				joint_.angularZMotion = ConfigurableJointMotion.Free;

joint_.rotationDriveMode = RotationDriveMode.XYAndZ;
joint_.angularXDrive = joint_.angularYZDrive = new JointDrive
			{
				//mode = JointDriveMode.PositionAndVelocity,
				positionSpring = 100f,
				positionDamper = 10f,
				maximumForce = 1000f
			};

//joint_.xDrive = joint_.yDrive = joint_.zDrive = new JointDrive	
//            {
//                //mode = JointDriveMode.PositionAndVelocity,
//                positionSpring = 10f,
//                positionDamper = 0.001f,
//                maximumForce = 100f
//            };

				//joint_.projectionAngle = 0f;
				//joint_.projectionDistance = 0f;
		//		joint_.anchor = anchorPosition;

//der Anker... der ist doch das Problem... nur um den dreht die Scheisse...


	//			joint_.targetPosition = anchorPosition;
	//			joint_.targetPosition = joint_.anchor;		-> damit bleibt's stehen wie's war... komisch, oder?
			}


// FEHLER, mal einfach so jetzt mal, verflixt du....

// der obere Joint dreht nur... der tut sonst nichts... der Haupt-Joint ist im Target und das muss sich auch bewegen... oder?

			Rigidbody tgt2 =
			targetBody.GetComponentInParent<Rigidbody>();

				joint2_ = targetBody.gameObject.AddComponent<ConfigurableJoint>();
				joint2_.connectedBody = tgt2;
				joint2_.breakForce = joint2_.breakTorque = Mathf.Infinity;
	//			joint_.xMotion = ConfigurableJointMotion.Locked;
	//			joint_.yMotion = ConfigurableJointMotion.Locked;
	//			joint_.zMotion = ConfigurableJointMotion.Locked;
	//			joint_.angularXMotion = ConfigurableJointMotion.Locked;
	//			joint_.angularYMotion = ConfigurableJointMotion.Locked;
	//			joint_.angularZMotion = ConfigurableJointMotion.Locked;

//				joint_.xMotion = ConfigurableJointMotion.Free;
//				joint_.yMotion = ConfigurableJointMotion.Free;
//				joint_.zMotion = ConfigurableJointMotion.Free;
				joint2_.angularXMotion = ConfigurableJointMotion.Free;
				joint2_.angularYMotion = ConfigurableJointMotion.Free;
				joint2_.angularZMotion = ConfigurableJointMotion.Free;

joint2_.rotationDriveMode = RotationDriveMode.XYAndZ;
joint2_.angularXDrive = joint2_.angularYZDrive = new JointDrive
			{
				//mode = JointDriveMode.PositionAndVelocity,
				positionSpring = 100f,
				positionDamper = 10f,
				maximumForce = 1000f
			};


			PlayAttachSound();
		}


		public void CreateJoint(Part originBody, Part targetBody, LinkType type, Vector3 anchorPosition)
		{
	//		LinkType = type;

			CreateJoint_(originBody, targetBody, type, anchorPosition);

			return;

			var breakForce = type.GetJointStrength();

	//		if(!IsEnforced) -> FEHLER, bei non-Enforced müssen wir ihn einfach weniger stark machen... wir bauen jetzt was total neues aus diesem Zeugs hier... mal sehen was es wird

			joint_ = originBody.GetComponent<Rigidbody>().gameObject.AddComponent<ConfigurableJoint>();
			joint_.connectedBody = targetBody.GetComponent<Rigidbody>();

			joint_.breakForce = joint_.breakTorque = Mathf.Infinity;

	//		joint_.xMotion = ConfigurableJointMotion.Locked;
	//		joint_.yMotion = ConfigurableJointMotion.Locked;
	//		joint_.zMotion = ConfigurableJointMotion.Locked;
	//		joint_.angularXMotion = ConfigurableJointMotion.Locked;
	//		joint_.angularYMotion = ConfigurableJointMotion.Locked;
	//		joint_.angularZMotion = ConfigurableJointMotion.Locked;

//			joint_.xMotion = ConfigurableJointMotion.Free;
//			joint_.yMotion = ConfigurableJointMotion.Free;
//			joint_.zMotion = ConfigurableJointMotion.Free;
			joint_.angularXMotion = ConfigurableJointMotion.Free;
			joint_.angularYMotion = ConfigurableJointMotion.Free;
			joint_.angularZMotion = ConfigurableJointMotion.Free;

			joint_.rotationDriveMode = RotationDriveMode.XYAndZ;
			joint_.angularXDrive = joint_.angularYZDrive = new JointDrive
				{
					//mode = JointDriveMode.PositionAndVelocity,
					positionSpring = 0f,
					positionDamper = 0f,
					maximumForce = 0f
				};

//			joint_.xDrive = joint_.yDrive = joint_.zDrive = new JointDrive	
//				{
					//mode = JointDriveMode.PositionAndVelocity,
//					positionSpring = 10f,
//					positionDamper = 0.001f,
//					maximumForce = 100f
//				};

			joint_.projectionAngle = 0f;
			joint_.projectionDistance = 0f;
//			joint_.anchor = anchorPosition;

//der Anker... der ist doch das Problem... nur um den dreht die Scheisse...

//			joint_.targetPosition = anchorPosition;
//			joint_.targetPosition = joint_.anchor;		-> damit bleibt's stehen wie's war... komisch, oder?


// FEHLER, mal einfach so jetzt mal, verflixt du....

// der obere Joint dreht nur... der tut sonst nichts... der Haupt-Joint ist im Target und das muss sich auch bewegen... oder?

return;

			joint2_ = targetBody.gameObject.AddComponent<ConfigurableJoint>();
			joint2_.connectedBody = targetBody.parent.Rigidbody;

			joint2_.breakForce = joint2_.breakTorque = Mathf.Infinity;
	//		joint_.xMotion = ConfigurableJointMotion.Locked;
	//		joint_.yMotion = ConfigurableJointMotion.Locked;
	//		joint_.zMotion = ConfigurableJointMotion.Locked;
	//		joint_.angularXMotion = ConfigurableJointMotion.Locked;
	//		joint_.angularYMotion = ConfigurableJointMotion.Locked;
	//		joint_.angularZMotion = ConfigurableJointMotion.Locked;

			joint2_.xMotion = ConfigurableJointMotion.Free;
			joint2_.yMotion = ConfigurableJointMotion.Free;
			joint2_.zMotion = ConfigurableJointMotion.Free;
			joint2_.angularXMotion = ConfigurableJointMotion.Free;
			joint2_.angularYMotion = ConfigurableJointMotion.Free;
			joint2_.angularZMotion = ConfigurableJointMotion.Free;

			joint2_.rotationDriveMode = RotationDriveMode.XYAndZ;
			joint2_.angularXDrive = joint2_.angularYZDrive = new JointDrive
				{
					//mode = JointDriveMode.PositionAndVelocity,
					positionSpring = 100f,
					positionDamper = 10f,
					maximumForce = 1000f
				};

			joint2_.xDrive = joint2_.yDrive = joint2_.zDrive = new JointDrive	
				{
					//mode = JointDriveMode.PositionAndVelocity,
					positionSpring = 0f,
					positionDamper = 0f,
					maximumForce = 0f
				};

			PlayAttachSound();
		}


		public void CreateJoint_(Part originBody, Part targetBodyChild, LinkType type, Vector3 anchorPosition)
		{
// bauen wir mal ein Objekt auf...

			var test = GameObject.CreatePrimitive(PrimitiveType.Cube);
			if(test.GetComponent<Rigidbody>() == null)
				test.AddComponent<Rigidbody>();
			test.name = "der_Idiot_halt";
			DestroyImmediate(test.GetComponent<Collider>()); // von mir aus...
			const float LOCAL_ANCHOR_DIM = 0.05f;
			test.transform.localScale = new Vector3(LOCAL_ANCHOR_DIM, LOCAL_ANCHOR_DIM, LOCAL_ANCHOR_DIM);
			var mr = test.GetComponent<MeshRenderer>();
			mr.name = test.name;
			mr.material = new Material(Shader.Find("Diffuse")) {color = Color.magenta};
			test.GetComponent<Rigidbody>().mass = 0.01f;
			test.SetActive(true);


			test.transform.position = originBody.transform.position +
				(targetBodyChild.transform.position - originBody.transform.position); // / 2;


			ConfigurableJoint joint = test.AddComponent<ConfigurableJoint>();
				
				
			joint.connectedBody = targetBodyChild.GetComponent<Rigidbody>();


Vector3 v1, v2, v3;
			v1 = originBody.transform.position - test.transform.position;
			v2 = Vector3.up;
			v3 = Vector3.right;

Vector3.OrthoNormalize(ref v1, ref v2, ref v3);

joint.axis = v1; joint.secondaryAxis = v2;



				joint.breakForce = joint.breakTorque = Mathf.Infinity;
				joint.xMotion = ConfigurableJointMotion.Locked;
				joint.yMotion = ConfigurableJointMotion.Locked;
				joint.zMotion = ConfigurableJointMotion.Locked;
				joint.angularXMotion = ConfigurableJointMotion.Locked;
				joint.angularYMotion = ConfigurableJointMotion.Locked;
				joint.angularZMotion = ConfigurableJointMotion.Locked;

				joint.xMotion = ConfigurableJointMotion.Free;
				joint.yMotion = ConfigurableJointMotion.Free;
				joint.zMotion = ConfigurableJointMotion.Free;
				joint.angularXMotion = ConfigurableJointMotion.Free;
				joint.angularYMotion = ConfigurableJointMotion.Free;
				joint.angularZMotion = ConfigurableJointMotion.Free;

joint.rotationDriveMode = RotationDriveMode.XYAndZ;
joint.angularXDrive = joint.angularYZDrive = new JointDrive
			{
				//mode = JointDriveMode.PositionAndVelocity,
				positionSpring = 0f,
				positionDamper = 0f,
				maximumForce = 0f
			};

joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked; // wir haben rausgefunden, dass es das nicht braucht -> bzw. entweder das hier Free und die nachfolgenden Settings oder das hier Locked... -> weil am Grappler will ich ja nur die Drehung erlauben

// ok, das hier würde, wäre alles "free" den Mist in der korrekten Position halten -> wäre also sowas wie eine "Feder" am Grappler
joint.xMotion = ConfigurableJointMotion.Free;
joint.xDrive =
//	joint.yDrive = joint.zDrive =
	new JointDrive	
			{
				//mode = JointDriveMode.PositionAndVelocity,
				positionSpring = 4f, // 10f,
				positionDamper = 0.5f, // 0.001f,
				maximumForce = 10f // 100f
			};

				joint.projectionAngle = 0f;
				joint.projectionDistance = 0f;
//				joint.targetPosition = anchorPosition;
//				joint.anchor = anchorPosition;



	joint.targetPosition = Vector3.right // move always along x axis!!
		* (originBody.transform.position - test.transform.position).magnitude // / 2;


			// >>>>>> mit dem Schmarrn da oben erzeuge ich eigentlich nochmal ein Kettenglied... mal sehen wie's läuft




bool iLikeThis = false;
if(iLikeThis)
{
			if(HighLogic.LoadedSceneIsFlight && part != null && part.attachJoint != null &&
				part.attachJoint.Joint != null)
			{
				part.attachJoint.Joint.breakForce = Mathf.Infinity;
				part.attachJoint.Joint.breakTorque = Mathf.Infinity;
				if(!IsFreeAttached && Target != null && Target.part != null && Target.part.attachJoint != null &&
					Target.part.attachJoint.Joint != null)
				{
					Target.part.attachJoint.Joint.breakForce = Mathf.Infinity;
					Target.part.attachJoint.Joint.breakTorque = Mathf.Infinity;
				}
			}
}

			LinkType = type;
			var breakForce = type.GetJointStrength();

			if(!IsFreeAttached)
			{
				var moduleActiveStrut = Target;
				if(moduleActiveStrut != null)
				{
					moduleActiveStrut.LinkType = type;
					IsOwnVesselConnected = moduleActiveStrut.vessel == vessel;
				}
			}
			else
				IsOwnVesselConnected = FreeAttachPart.vessel == vessel;

dd
			//var localAnchor = GameObject.CreatePrimitive(PrimitiveType.Cube);
			//if(localAnchor.GetComponent<Rigidbody>() == null)
			//    localAnchor.AddComponent<Rigidbody>();
			//localAnchor.name = name;
			//Object.DestroyImmediate(localAnchor.GetComponent<Collider>());
			//const float LOCAL_ANCHOR_DIM = 0.000001f;
			//localAnchor.transform.localScale = new Vector3(LOCAL_ANCHOR_DIM, LOCAL_ANCHOR_DIM, LOCAL_ANCHOR_DIM);
			//var mr = localAnchor.GetComponent<MeshRenderer>();
			//mr.name = name;
			//mr.material = new Material(Shader.Find("Diffuse")) {color = Color.magenta};
			//localAnchor.GetComponent<Rigidbody>().mass = 0.00001f;
			//localAnchor.SetActive(active);
			//return localAnchor;
dd
	//		if(!IsEnforced) -> FEHLER, bei non-Enforced müssen wir ihn einfach weniger stark machen... wir bauen jetzt was total neues aus diesem Zeugs hier... mal sehen was es wird
			{
				joint_ = originBody.GetComponent<Rigidbody>().gameObject.AddComponent<ConfigurableJoint>();
			//	joint_.connectedBody = targetBodyChild.parent.GetComponent<Rigidbody>(); -> so würde das wohl gehen... jetzt probier ich aber was anderes
//				joint_.connectedBody = targetBodyChild.GetComponent<Rigidbody>();
joint_.connectedBody = test.GetComponent<Rigidbody>();

				joint_.breakForce = joint_.breakTorque = Mathf.Infinity;
				joint_.xMotion = ConfigurableJointMotion.Locked;
				joint_.yMotion = ConfigurableJointMotion.Locked;
				joint_.zMotion = ConfigurableJointMotion.Locked;
				joint_.angularXMotion = ConfigurableJointMotion.Locked;
				joint_.angularYMotion = ConfigurableJointMotion.Locked;
				joint_.angularZMotion = ConfigurableJointMotion.Locked;

				joint_.xMotion = ConfigurableJointMotion.Free;
				joint_.yMotion = ConfigurableJointMotion.Free;
				joint_.zMotion = ConfigurableJointMotion.Free;
				joint_.angularXMotion = ConfigurableJointMotion.Free;
				joint_.angularYMotion = ConfigurableJointMotion.Free;
				joint_.angularZMotion = ConfigurableJointMotion.Free;

joint_.rotationDriveMode = RotationDriveMode.XYAndZ;
joint_.angularXDrive = joint_.angularYZDrive = new JointDrive
			{
				//mode = JointDriveMode.PositionAndVelocity,
				positionSpring = 0f,
				positionDamper = 0f,
				maximumForce = 0f
			};

joint_.xMotion = joint_.yMotion = joint_.zMotion = ConfigurableJointMotion.Locked; // wir haben rausgefunden, dass es das nicht braucht -> bzw. entweder das hier Free und die nachfolgenden Settings oder das hier Locked... -> weil am Grappler will ich ja nur die Drehung erlauben

// ok, das hier würde, wäre alles "free" den Mist in der korrekten Position halten -> wäre also sowas wie eine "Feder" am Grappler
joint_.xDrive = joint_.yDrive = joint_.zDrive = new JointDrive	
			{
				//mode = JointDriveMode.PositionAndVelocity,
				positionSpring = 1e30f, // 10f,
				positionDamper = 10f, // 0.001f,
				maximumForce = 1e6f // 100f
			};

				joint_.projectionAngle = 0f;
				joint_.projectionDistance = 0f;
//				joint.targetPosition = anchorPosition;
//				joint.anchor = anchorPosition;
			}

			if(false) // wir machen das neu mit dem neuen objekt, das wir dazwischen gespannt haben
			{
	//			joint2_ = targetBodyChild.GetComponent<Rigidbody>().gameObject.AddComponent<ConfigurableJoint>();
	//			joint2_.connectedBody = targetBodyChild.parent.GetComponent<Rigidbody>(); -> so würde das wohl gehen... jetzt probier ich aber was anderes
	//			joint2_.connectedBody = targetBodyChild.GetComponent<Rigidbody>();
joint2_ = targetBodyChild.attachJoint.Joint; // ja, den gibt's in dem Fall ja schon

				joint2_.breakForce = joint2_.breakTorque = Mathf.Infinity;
				joint2_.xMotion = ConfigurableJointMotion.Locked;
				joint2_.yMotion = ConfigurableJointMotion.Locked;
				joint2_.zMotion = ConfigurableJointMotion.Locked;
				joint2_.angularXMotion = ConfigurableJointMotion.Locked;
				joint2_.angularYMotion = ConfigurableJointMotion.Locked;
				joint2_.angularZMotion = ConfigurableJointMotion.Locked;

				joint2_.xMotion = ConfigurableJointMotion.Free;
				joint2_.yMotion = ConfigurableJointMotion.Free;
				joint2_.zMotion = ConfigurableJointMotion.Free;
				joint2_.angularXMotion = ConfigurableJointMotion.Free;
				joint2_.angularYMotion = ConfigurableJointMotion.Free;
				joint2_.angularZMotion = ConfigurableJointMotion.Free;

joint2_.rotationDriveMode = RotationDriveMode.XYAndZ;
joint2_.angularXDrive = joint2_.angularYZDrive = new JointDrive
			{
				//mode = JointDriveMode.PositionAndVelocity,
				positionSpring = 0f,
				positionDamper = 0f,
				maximumForce = 0f
			};

//joint2_.axis = joint_.transform.position - joint2_.transform.position;
//Vector3 vn = joint2_.axis;
//Vector3 v1 = new Vector3(), v2 = new Vector3(); Vector3.OrthoNormalize(ref vn, ref v1, ref v2);
//joint2_.secondaryAxis = v1;

joint2_.xMotion = joint2_.yMotion = joint2_.zMotion = ConfigurableJointMotion.Locked; // wir haben rausgefunden, dass es das nicht braucht -> bzw. entweder das hier Free und die nachfolgenden Settings oder das hier Locked... -> weil am Grappler will ich ja nur die Drehung erlauben


//// jetzt soll's mal raufziehen
//joint2_.xMotion = ConfigurableJointMotion.Free;
//joint2_.targetPosition = joint_.transform.position - joint2_.transform.position;

//// ok, das hier würde, wäre alles "free" den Mist in der korrekten Position halten -> wäre also sowas wie eine "Feder" am Grappler
//joint2_.xDrive = joint2_.yDrive = joint2_.zDrive = new JointDrive	
//            {
//                //mode = JointDriveMode.PositionAndVelocity,
//                positionSpring = 10f, // 10f,
//                positionDamper = 5f, // 0.001f,
//                maximumForce = 100f // 100f
//            };

				joint2_.projectionAngle = 0f;
				joint2_.projectionDistance = 0f;
//				joint.targetPosition = anchorPosition;
//				joint.anchor = anchorPosition;
			}

			PlayAttachSound();
		}

		public void DestroyJoint()
		{
			DestroyImmediate(joint_);
			joint_ = null;
if(joint2_ != null)
DestroyImmediate(joint2_);
joint2_ = null;
			LinkType = LinkType.None;
			if(IsDocked)
				ProcessUnDock(true);
			UpdateSimpleLights();
		}

		public void CreateStrut(Vector3 target, float distancePercent = 1, float strutOffset = 0f)
		{
			if(ModelFeatures[ModelFeaturesType.Animation] && IsAnimationPlaying)
				return;

			if(Target != null && Target.ModelFeatures[ModelFeaturesType.Animation] && Target.IsAnimationPlaying)
				return;

			if(Targeter != null && Targeter.ModelFeatures[ModelFeaturesType.Animation] && Targeter.IsAnimationPlaying)
				return;

			var strut = Strut;
			strut.LookAt(target);
			strut.Rotate(new Vector3(0, 1, 0), 90f);
			strut.localScale = new Vector3(1, 1, 1);

			var distance = (Vector3.Distance(Vector3.zero, Strut.InverseTransformPoint(target)) * distancePercent *
							StrutScaleFactor) + strutOffset; //*-1

			if(IsFreeAttached)
				distance += Config.Instance.FreeAttachStrutExtension;

			Strut.localScale = new Vector3(distance, 1, 1);
			TransformLights(true, target, IsDocked);

			if(ModelFeatures[ModelFeaturesType.HeadExtension])
				headTransform.LookAt(target);

			strutFinallyCreated = true;
		}

		public void DestroyStrut()
		{
			Strut.localScale = Vector3.zero;
			ShowGrappler(false, Vector3.zero, Vector3.zero, false, Vector3.zero);
			TransformLights(false, Vector3.zero);
			strutFinallyCreated = false;
		}

		private void DeployHead(float speed)
		{
			AniExtended = true;
			PlayDeployAnimation(speed);
		}

		[KSPEvent(name = "Dock", active = false, guiName = "Dock with Target", guiActiveEditor = false,
			guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
		public void Dock()
		{
			if(HighLogic.LoadedSceneIsEditor || !IsLinked || !IsConnectionOrigin || IsTargetOnly ||
				IsOwnVesselConnected || (IsFreeAttached ? FreeAttachPart == null : Target == null) || IsDocked)
			{
				OSD.PostMessage("Can't dock.");
				return;
			}
			if(IsFreeAttached
				? FreeAttachPart != null && FreeAttachPart.vessel == vessel
				: Target != null && Target.part != null && Target.part.vessel == vessel)
			{
				OSD.PostMessage("Already docked");
				return;
			}
			DockingVesselName = vessel.GetName();
			DockingVesselTypeString = vessel.vesselType.ToString();
			DockingVesselId = vessel.rootPart.flightID;
			IsDocked = true;
			if(IsFreeAttached)
			{
				var attachPart = FreeAttachPart;
				if(attachPart != null)
					attachPart.Couple(part);
			}
			else
			{
				var moduleActiveStrut = Target;
				if(moduleActiveStrut != null)
					moduleActiveStrut.part.Couple(part);
			}
			UpdateGui();
			foreach(var moduleActiveStrut in Utilities.GetAllActiveStruts())
				moduleActiveStrut.UpdateGui();
			OSD.PostMessage("Docked.");
		}

		[KSPEvent(name = "FreeAttach", active = false, guiActiveEditor = false, guiName = "FreeAttach Link",
			guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
		public void FreeAttach()
		{
			if(HighLogic.LoadedSceneIsEditor)
			{
				InputLockManager.SetControlLock(EDITOR_LOCK_MASK, Config.Instance.EditorInputLockId);
				var newPart = PartFactory.SpawnPartInEditor("ASTargetCube");
				Debug.Log("[IRAS] spawned part in editor");
				ActiveStrutsAddon.CurrentTargeter = this;
				ActiveStrutsAddon.Mode = AddonMode.FreeAttach;
				ActiveStrutsAddon.NewSpawnedPart = newPart;
			}
			StraightOutAttachAppliedInEditor = false;
			if(Config.Instance.ShowHelpTexts)
				OSD.PostMessage(Config.Instance.FreeAttachHelpText, 5);
			if(HighLogic.LoadedSceneIsFlight)
				StartCoroutine(PreparePartForFreeAttach());
		}

		[KSPEvent(name = "FreeAttachStraight", active = false, guiName = "Straight Up FreeAttach",
			guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
		public void FreeAttachStraight()
		{
			var raycast = _performStraightOutRaycast();
			if(raycast.Item1)
			{
				var hittedPart = raycast.Item2.PartFromHit();
				var valid = hittedPart != null;
				if(valid)
				{
					if(HighLogic.LoadedSceneIsEditor)
					{
						StraightOutAttachAppliedInEditor = true;
						IsLinked = true;
						IsFreeAttached = true;
						UpdateGui();
						straightOutAttached = true;
						return;
					}
					StraightOutAttachAppliedInEditor = false;
					IsLinked = false;
					IsFreeAttached = false;
					straightOutAttached = false;
					if(HighLogic.LoadedSceneIsFlight)
					{
						//StartCoroutine(PreparePartForFreeAttach(true));
						PlaceFreeAttach(hittedPart, true);
						straightOutAttached = true;
					}
				}
			}
			else
				OSD.PostMessage("Nothing has been hit.");
		}

		[KSPAction("FreeAttachStraightAction", KSPActionGroup.None, guiName = "Straight Up FreeAttach")]
		public void FreeAttachStraightAction(KSPActionParam param)
		{
			if(Mode == Mode.Unlinked && !IsTargetOnly)
				FreeAttachStraight();
		}

		public override void OnStart(StartState state)
		{
DebugInit();

Origin = part.transform;
			_findModelFeatures();

if(_position == Vector3.zero)
	_position = Grappler.position - part.transform.position;

			if(ModelFeatures[ModelFeaturesType.SimpleLights])
				UpdateSimpleLights();

			if(ModelFeatures[ModelFeaturesType.Animation])
			{
				if(AniExtended)
					DeployHead(FAST_ANI_SPEED);
				else
					RetractHead(FAST_ANI_SPEED);
			}

			if(!IsTargetOnly)
			{
				if(ModelFeatures[ModelFeaturesType.LightsBright] || ModelFeatures[ModelFeaturesType.LightsDull])
					LightsOffset *= 0.5f;
				DestroyStrut();
			}

			if(HighLogic.LoadedSceneIsEditor)
				part.OnEditorAttach += ProcessOnPartCopy;
//			Origin = part.transform;
			delayedStartFlag = true;
			ticksForDelayedStart = HighLogic.LoadedSceneIsEditor ? 0 : Config.Instance.StartDelay;
			strutRealignCounter = Config.Instance.StrutRealignInterval*(HighLogic.LoadedSceneIsEditor ? 3 : 0);

			initialized = true;
		}

		public void PlaceFreeAttach(Part targetPart, bool isStraightOut = false)
		{
			lock (freeAttachStrutUpdateLock)
			{
				oldTargetPosition = Vector3.zero;
				ActiveStrutsAddon.Mode = AddonMode.None;

				freeAttachPart = targetPart;
				Mode = Mode.Linked;
				IsLinked = true;
				IsFreeAttached = true;
				IsConnectionOrigin = true;
				DestroyJoint();
				DestroyStrut();
				IsEnforced = Config.Instance.GlobalJointEnforcement;
				if(HighLogic.LoadedSceneIsFlight)
				{
					CreateJoint(part.Rigidbody, (IsFreeAttached && !isStraightOut) ? targetPart.Rigidbody : targetPart.Rigidbody,
						LinkType.Weak, targetPart.transform.position);
				}
				Target = null;
				Targeter = null;
				DeployHead(NORMAL_ANI_SPEED);
				OSD.PostMessage("FreeAttach Link established!");
			}
			UpdateGui();
		}


		private void PlayDeployAnimation(float speed)
		{
			if(!ModelFeatures[ModelFeaturesType.Animation])
				return;
			var ani = DeployAnimation;
			if(ani == null)
			{
				Debug.Log("[IRAS] animation is null!");
				return;
			}
			if(IsAnimationPlaying)
				ani.Stop(AnimationName);
			if(!AniExtended)
				speed *= -1;
			if(speed < 0)
				ani[AnimationName].time = ani[AnimationName].length;
			ani[AnimationName].speed = speed;
			ani.Play(AnimationName);
		}

		private IEnumerator PreparePartForFreeAttach(bool straightOut = false, int tryCount = 0)
		{
			const int MAX_WAITS = 30;
			const int MAX_TRIES = 5;
			var currWaits = 0;
			var newPart = PartFactory.SpawnPartInFlight("ASTargetCube", part, new Vector3(2, 2, 2),
				part.transform.rotation);
			OSD.PostMessageLowerRightCorner("waiting for Unity to catch up...", 1.5f);
			while(!newPart.GetComponent<Rigidbody>() && currWaits < MAX_WAITS && newPart.vessel != null)
			{
				Debug.Log("[IRAS] rigidbody not ready - waiting");
				currWaits++;
				try
				{
					DestroyImmediate(newPart.collider);
				}
				catch (Exception)
				{
					//sanity reason
				}
				try
				{
					newPart.transform.position = part.transform.position;
				}
				catch (NullReferenceException)
				{
					//sanity reason
				}
				try
				{
					newPart.mass = 0.000001f;
					newPart.maximum_drag = 0f;
					newPart.minimum_drag = 0f;
				}
				catch (NullReferenceException)
				{
					//sanity reason
				}
				yield return new WaitForFixedUpdate();
			}
			if(newPart.vessel == null || (MAX_WAITS == currWaits && newPart.GetComponent<Rigidbody>() == null))
			{
				if(tryCount < MAX_TRIES)
				{
					var nextTryCount = ++tryCount;
					Debug.Log(
						string.Format("[IRAS] part spawning failed => retry (vessel is null = {0} | waits = {1}/{2})",
							(newPart.vessel == null), currWaits, MAX_WAITS));
					StartCoroutine(PreparePartForFreeAttach(straightOut, nextTryCount));
				}
				else
				{
					Debug.Log(
						string.Format(
							"[IRAS] part spawning failed more than {3} times => aborting FreeAttach (vessel is null = {0} | waits = {1}/{2})",
							(newPart.vessel == null), currWaits, MAX_WAITS, MAX_TRIES));
					OSD.PostMessage("FreeAttach failed because target part can not be prepared!");
					try
					{
						AbortLink();
					}
					catch (NullReferenceException e)
					{
						Debug.Log("[IRAS] tried to abort link because part spawning failed, but abort throw exception: " +
								  e.Message);
					}
				}
				try
				{
					newPart.Die();
					Destroy(newPart);
				}
				catch (Exception e)
				{
					Debug.Log(
						"[IRAS] tried to destroy a part which failed to spawn properly in time, but operation throw exception: " +
						e.Message);
				}
				yield break;
			}
			newPart.mass = 0.000001f;
			newPart.maximum_drag = 0f;
			newPart.minimum_drag = 0f;
			if(straightOut)
				_continueWithStraightOutAttach(newPart);
			else
			{
				ActiveStrutsAddon.NewSpawnedPart = newPart;
				ActiveStrutsAddon.CurrentTargeter = this;
				ActiveStrutsAddon.Mode = AddonMode.FreeAttach;
			}
		}

		public void ProcessOnPartCopy()
		{
			var allModules = Utilities.GetAllActiveStruts();
			if(allModules != null && allModules.Any(m => m.ID == ID))
				ResetActiveStrutToDefault();
			else
			{
				Unlink();
				Update();
			}
		}

		private void ProcessUnDock(bool undockByUnlink = false)
		{
			if(HighLogic.LoadedSceneIsEditor || (!IsLinked && !undockByUnlink) || !IsConnectionOrigin || IsTargetOnly ||
				(IsOwnVesselConnected && !IsDocked) ||
				(IsFreeAttached ? FreeAttachPart == null : Target == null) ||
				!IsDocked)
			{
				OSD.PostMessage("Can't undock");
				return;
			}
			var vi = new DockedVesselInfo
			{
				name = DockingVesselName,
				rootPartUId = DockingVesselId,
				vesselType = (VesselType) Enum.Parse(typeof (VesselType), DockingVesselTypeString)
			};
			IsDocked = false;
			if(IsFreeAttached)
				FreeAttachPart.Undock(vi);
			else
				Target.part.Undock(vi);
			UpdateGui();
			OSD.PostMessage("Undocked");
		}

		public void ProcessUnlink(bool fromUserAction, bool secondary)
		{
			StraightOutAttachAppliedInEditor = false;
			straightOutAttached = false;
			if(AniExtended)
				RetractHead(NORMAL_ANI_SPEED);
			if(!IsTargetOnly && (Target != null || Targeter != null))
			{
				if(!IsConnectionOrigin && !secondary && Targeter != null)
				{
					try
					{
						Targeter.Unlink();
					}
					catch (NullReferenceException)
					{
						//fail silently
					}
					return;
				}
				if(IsFreeAttached)
					IsFreeAttached = false;
				Mode = Mode.Unlinked;
				IsLinked = false;
				DestroyJoint();
		//		DestroyStrut();
				oldTargetPosition = Vector3.zero;
				LinkType = LinkType.None;
				if(IsConnectionOrigin)
				{
					if(Target != null)
					{
						try
						{
							Target.ProcessUnlink(false, true);
							if(HighLogic.LoadedSceneIsEditor)
							{
								Target.Targeter = null;
								Target = null;
							}
						}
						catch (NullReferenceException)
						{
							//fail silently
						}
					}
			//		if(!fromUserAction && HighLogic.LoadedSceneIsEditor)
					if(fromUserAction && !HighLogic.LoadedSceneIsEditor)
					{
						OSD.PostMessage("Unlinked!");
						PlayDetachSound();

// FEHLER, wir probieren mal zurückzuziehen hier...
//	neue idee... ausklinken und stehen bleiben

			Grappler.localScale = Vector3.one;

			StartCoroutine(ComeBack());
					}
				}
				IsConnectionOrigin = false;
				UpdateGui();
				return;
			}
			if(IsTargetOnly)
			{
				if(!this.AnyTargetersConnected())
				{
					Mode = Mode.Unlinked;
					IsLinked = false;
				}
				UpdateGui();
				return;
			}
			var destroyTarget = false;
			if(IsFreeAttached)
			{
				IsFreeAttached = false;
				destroyTarget = true;
			}
			oldTargetPosition = Vector3.zero;
			Mode = Mode.Unlinked;
			IsLinked = false;
	//		DestroyStrut();
			DestroyJoint();
			LinkType = LinkType.None;
			UpdateGui();
			if(!fromUserAction && HighLogic.LoadedSceneIsEditor)
			{
				OSD.PostMessage("Unlinked!");
				PlayDetachSound();
			}
		}

		private void Reconnect()
		{
			if(StraightOutAttachAppliedInEditor)
			{
				FreeAttachStraight();
				return;
			}
			if(IsFreeAttached)
			{
				IsFreeAttached = false;
				Mode = Mode.Unlinked;
				IsConnectionOrigin = false;
				LinkType = LinkType.None;
				UpdateGui();
				return;
			}
			var unlink = false;
			if(IsConnectionOrigin)
			{
				if(Target != null && this.IsPossibleTarget(Target))
				{
					if(!Target.IsTargetOnly)
					{
						CreateStrut(
							Target.ModelFeatures[ModelFeaturesType.HeadExtension]
								? Target.StrutOrigin.position
								: Target.Origin.position, 0.5f);
					}
					else
					{
						CreateStrut(Target.ModelFeatures[ModelFeaturesType.HeadExtension]
							? Target.StrutOrigin.position
							: Target.Origin.position);
					}
					var type = Target.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
					IsEnforced = Config.Instance.GlobalJointEnforcement || type == LinkType.Maximum;
					CreateJoint(part.GetComponent<Rigidbody>(), Target.part.parent.GetComponent<Rigidbody>(), type, Target.transform.position);
					Mode = Mode.Linked;
					Target.Mode = Mode.Linked;
					IsLinked = true;
				}
				else
					unlink = true;
			}
			else
			{
				if(IsTargetOnly)
				{
					Mode = Mode.Linked;
					IsLinked = true;
				}
				else if(Targeter != null && this.IsPossibleTarget(Targeter))
				{
					CreateStrut(
						Targeter.ModelFeatures[ModelFeaturesType.HeadExtension]
							? Targeter.StrutOrigin.position
							: Targeter.Origin.position, 0.5f);
					LinkType = LinkType.Maximum;
					Mode = Mode.Linked;
					IsLinked = true;
				}
				else
					unlink = true;
			}
			if(unlink)
				Unlink();
			UpdateGui();
		}

		private void ResetActiveStrutToDefault()
		{
			Target = null;
			Targeter = null;
			IsConnectionOrigin = false;
			IsFreeAttached = false;
			Mode = Mode.Unlinked;
			IsHalfWayExtended = false;
			Id = Guid.NewGuid().ToString();
			LinkType = LinkType.None;
			OldTargeter = null;
			IsFreeAttached = false;
			IsLinked = false;
			if(!IsTargetOnly)
			{
				DestroyJoint();
				DestroyStrut();
			}
		}

		private void RetractHead(float speed)
		{
			AniExtended = false;
			PlayDeployAnimation(speed);
		}

		[KSPEvent(name = "SetAsTarget", active = false, guiName = "Set as Target", guiActiveEditor = false,
			guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
		public void SetAsTarget()
		{
			IsLinked = true;
			part.SetHighlightDefault();
			Mode = Mode.Linked;
			IsConnectionOrigin = false;
			IsFreeAttached = false;
			if(!IsTargetOnly)
			{
				if(ModelFeatures[ModelFeaturesType.Animation])
					DeployHead(NORMAL_ANI_SPEED);
				CreateStrut(
					Targeter.ModelFeatures[ModelFeaturesType.HeadExtension]
						? Targeter.StrutOrigin.position
						: Targeter.Origin.position, 0.5f);
			}
			Targeter.SetTarget(this);
			UpdateGui();
		}

		public void SetTarget(ModuleActiveStrut target)
		{
			if(nurzielen)
			{
				ZielMal(target);
				return;
			}
		}

		public void _SetTarget(ModuleActiveStrut target)
		{
			if(ModelFeatures[ModelFeaturesType.Animation] && !AniExtended)
				DeployHead(NORMAL_ANI_SPEED);
			Target = target;
			Mode = Mode.Linked;
			IsLinked = true;
			IsConnectionOrigin = true;
			var type = target.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
			IsEnforced = Config.Instance.GlobalJointEnforcement || type == LinkType.Maximum;
			CreateJoint(part, target.part, type, Target.transform.position);
			CreateStrut(
				target.ModelFeatures[ModelFeaturesType.HeadExtension]
					? target.StrutOrigin.position
					: target.Origin.position, target.IsTargetOnly ? 1 : 0.5f);
			Utilities.ResetAllFromTargeting();
			OSD.PostMessage("Link established!");
			ActiveStrutsAddon.Mode = AddonMode.None;
			UpdateGui();
		}

		public void SetTargetedBy(ModuleActiveStrut targeter)
		{
			OldTargeter = Targeter;
			Targeter = targeter;
			Mode = Mode.Target;
		}

		public void ShowGrappler(bool show, Vector3 targetPos, Vector3 lookAtPoint, bool applyOffset,
			Vector3 targetNormalVector, bool useNormalVector = false, bool inverseOffset = false)
		{
			if(Grappler == null || ModelFeatures == null)
				return;

			if(!show)
			{
				Grappler.localScale = Vector3.zero;
				return;
			}

			if(!ModelFeatures[ModelFeaturesType.Grappler] )
				return;

			if(show && !IsTargetOnly)
			{
				Grappler.localScale = new Vector3(1, 1, 1);
				Grappler.position = Origin.position;
				Grappler.LookAt(lookAtPoint);
				Grappler.position = targetPos;
				Grappler.Rotate(new Vector3(0, 1, 0), 90f);
				if(useNormalVector)
					Grappler.rotation = Quaternion.FromToRotation(Grappler.right, targetNormalVector)*Grappler.rotation;
				if(applyOffset)
				{
					var offset = inverseOffset ? -1*GrapplerOffset : GrapplerOffset;
					Grappler.Translate(new Vector3(offset, 0, 0));
				}
			}
		}

// FEHLER, lauter Tests jetzt mal... -> neu ist das immer aktiv
private bool nurzielen = true;

//		[KSPEvent(name = "test show", guiName = "test show", guiActive = true, guiActiveEditor = true, active = true,
//			guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "test Arsch")]
		public void testshow()
		{
			// das Zeug anzeigen... geht super...

			Grappler.localScale = Vector3.one;
			Strut.localScale = Vector3.one;
			LightsDull.localScale = Vector3.one;
		//	LightsBright;

float hum = (Grappler.position - Origin.position).magnitude;
		}

		private void CalcRetracted()
		{
	//		Vector3 wohin = part.transform.position + part.transform.up; // damit würde er auch interessant zusammengeklappt aussehen
			Vector3 wohin = part.transform.position - part.transform.right;

			ZielMalDasDa(Strut, wohin);
			ZielMalDasDa(LightsDull, wohin);

			Grappler.position = Origin.position;
			Grappler.LookAt(wohin);
			Grappler.Rotate(new Vector3(0, 1, 0), 90f);
			Grappler.position = wohin;

			Grappler.position = Origin.position +
				(Grappler.position - Origin.position).normalized * GrapplerOriginalPosition.magnitude;
		}

		private void CalcDirection(Vector3 wohin)
		{
			Quaternion or = Strut.rotation;

			Strut.position = Origin.position;
			Strut.LookAt(wohin);
			Strut.Rotate(new Vector3(0, 1, 0), 90f); // FEHLER, wieso?

			Quaternion z = Strut.rotation;
	//		LightsDull.rotation = z * Quaternion.Inverse(or) * LightsDull.rotation;
	//		Grappler.rotation = z * Quaternion.Inverse(or) * Grappler.rotation;
			LightsDull.rotation = z;
			Grappler.rotation = z;		// FEHLER, sich das Zeug merken und vom original her rechnen, nur hab ich das hier nicht mehr...
		}

		private void CalcDistance(Vector3 wohin)
		{
			Strut.position = Origin.position;

			LightsDull.position = Origin.position +
				(wohin - Origin.position).normalized * LightsDullOriginalPosition.magnitude;

			Grappler.position = Origin.position +
				(wohin - Origin.position).normalized * GrapplerOriginalPosition.magnitude;


			float sf = (wohin - Origin.position).magnitude * StrutScaleFactor;
			Strut.localScale = new Vector3(sf, 1, 1);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "test Arsch2")]
		public void testshow2()
		{
			testshow();
	//		nurzielen = false;

			CalcRetracted();
		}

		private void ZielMalDasDa(Transform ItemTransform, Vector3 wohin)
		{
			Vector3 v = ItemTransform.position - Origin.position;
			Quaternion o = ItemTransform.rotation;

			ItemTransform.position = Origin.position;
			ItemTransform.LookAt(wohin);
			ItemTransform.Rotate(new Vector3(0, 1, 0), 90f); // FEHLER, wieso?

			ItemTransform.position = Origin.position + (ItemTransform.rotation * Quaternion.Inverse(o)) * v;
		}

Vector3 wohingo;
Vector3 startgo;

ModuleActiveStrut wohintarget;

		private void ZielMal(ModuleActiveStrut target)
		{
			wohintarget = target;

			//Vector3 wohin = wohintarget.transform.position;

			//// jetzt mal auf das geforderte Ziel zeigen... nur hinzeigen... nichts anderes...

			//ZielMalDasDa(Strut, wohin);
			//ZielMalDasDa(LightsDull, wohin);
			//ZielMalDasDa(Grappler, wohin);

			//startgo = Grappler.position; // hier starten wir... logo, oder?

//			Grappler.position = targetPos;

	//			if(applyOffset)
	//			{
	//				var offset = inverseOffset ? -1*GrapplerOffset : GrapplerOffset;
	//				Grappler.Translate(new Vector3(offset, 0, 0));
	//			}

		//	strut.localScale = new Vector3(1, 1, 1);

		//        var distance = (Vector3.Distance(Vector3.zero, Strut.InverseTransformPoint(refPos)) * 1 *
		//                        StrutScaleFactor); //*-1

		////		if(IsFreeAttached)
		////			distance += Config.Instance.FreeAttachStrutExtension;

		//        Strut.localScale = new Vector3(distance, 1, 1);


//	Vector3 wohin = wohintarget.transform.position;

//    Anchor.up
//        Vector3.ProjectOnPlane(wohin, part.transform.right)
//            part.transform.right


//DrawRelative(0, part.transform.position, Anchor.up);
//DrawRelative(1, part.transform.position, Vector3.ProjectOnPlane(wohin, part.transform.right));
//DrawRelative(2, part.transform.position, part.transform.right);


			StartCoroutine(GoGoGo());


	//		nurzielen = false;
		}

int ii = 0;
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "test Arsch3")]
		public void testshow3()
		{
			Quaternion q = Quaternion.identity;

			Vector3 dir = wohintarget.transform.position - Origin.position;

			switch(ii % 10)
			{
			case 0:
				break;
			case 1:
				dir = Quaternion.AngleAxis(90f, Vector3.right) * dir; break;
			case 2:
				dir = Quaternion.AngleAxis(180f, Vector3.right) * dir; break;
			case 3:
				dir = Quaternion.AngleAxis(270f, Vector3.right) * dir; break;
			case 4:
				dir = Quaternion.AngleAxis(90f, Vector3.up) * dir; break;
			case 5:
				dir = Quaternion.AngleAxis(180f, Vector3.up) * dir; break;
			case 6:
				dir = Quaternion.AngleAxis(270f, Vector3.up) * dir; break;
			case 7:
				dir = Quaternion.AngleAxis(90f, Vector3.forward) * dir; break;
			case 8:
				dir = Quaternion.AngleAxis(180f, Vector3.forward) * dir; break;
			case 9:
				dir = Quaternion.AngleAxis(270f, Vector3.forward) * dir; break;
			}

			q = Quaternion.LookRotation(dir);

			Strut.rotation = q;

			++ii;
		}

int gg = 35;

Vector3 wohintest;

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "test Schrott")]
		public void testSchrott()
		{
			Vector3 wohin = wohintest;

			float a2 = AngleSigned(Anchor.up,
				Vector3.ProjectOnPlane(wohin - part.transform.position, part.transform.right), part.transform.right);

			Anchor.rotation = Quaternion.AngleAxis(90 - a2, part.transform.right) * part.transform.rotation;
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "test Schrott2")]
		public void testSchrott2()
		{
			float a2 = 0;

			Anchor.rotation = Quaternion.AngleAxis(90 - a2, part.transform.right) * part.transform.rotation;
		}

		float MinimizeTurn(float angle)
		{
			while(angle > 90)
				angle -= 180;

			while(angle < -90)
				angle += 180;

			return angle;
		}

		private IEnumerator GoGoGo()
		{
			Vector3 wohin = wohintarget.transform.position;

			// zuerst die Rotation

			Vector3 globalAxis = part.transform.TransformDirection(axis);

			float angleBase =
				MinimizeTurn(
					90 + AngleSigned(
						part.transform.TransformDirection(_rot * pointer),
						Vector3.ProjectOnPlane(wohin - part.transform.position, globalAxis), globalAxis));

			Quaternion qangle_0 = _rot;

			Quaternion qangle_ = Quaternion.AngleAxis(-angleBase, axis);
			float angleBaseAbsolute = Mathf.Abs(angleBase);

			for(float angle = 0; angle + 1 < angleBaseAbsolute; angle += 1) // FEHLER, hier Begrenzer einbauen (nicht += 1), wenn wir zuweit draussen sind		
			{
				_position = Quaternion.Inverse(_rot) * _position;

				_rot = Quaternion.Slerp(qangle_0, qangle_, angle / angleBaseAbsolute);
				UpdateMeshPositions0();

				_position = _rot * _position;

yield return new WaitForSeconds(0.5f);
				yield return new WaitForFixedUpdate();
			}

			_position = Quaternion.Inverse(_rot) * _position;

			_rot = qangle_;
			UpdateMeshPositions0();

			_position = _rot * _position;


yield break; // nur mal drauf zeigen im Moment... ich will das perfektionieren... nix anderes

			// jetzt mal auf das geforderte Ziel zeigen... nur hinzeigen... nichts anderes...

			Vector3 globalPointer = part.transform.TransformDirection(_rot * pointer);

			float angleGrappler =
				AngleSigned(
					_position,
					wohin - Origin.position, globalPointer);

			Quaternion qangle0 = Quaternion.FromToRotation(axis.normalized, _position.normalized);

			Quaternion qangle = Quaternion.AngleAxis(-angleGrappler, _rot * pointer);
			float angleGrapplerAbsolute = Mathf.Abs(angleGrappler);

			for(float angle = 0; angle + 1 < angleGrapplerAbsolute; angle += 1)
			{
				_rot2 = Quaternion.Slerp(qangle0, qangle, angle / angleGrapplerAbsolute);
				_position = _rot2 * axis.normalized * _position.magnitude;
				UpdateMeshPositions();

				yield return new WaitForFixedUpdate();
			}

			_rot2 = qangle;
			_position = _rot2 * axis.normalized * _position.magnitude;
			UpdateMeshPositions();

			yield return new WaitForFixedUpdate();





			startgo = Grappler.position; // hier starten wir... logo, oder?

			wohingo = wohin;
	


			// das ist keine Rotation hier... jetzt ist das mal nur ein... direktes drauf gehen
			// später natürlich beides gleichzeitig machen oder sowas...

// FEHLER, wie komm ich später wieder zum orginal Zustand zurück?? tja, muss ich mir wohl merken das...

			float totdist = (wohingo - startgo).magnitude;

float speed = 0.005f; // pro Frame...

			float pos = 0;

			Quaternion q = quat(wohintarget.transform, CalculateBestHeadTurn());

			while(pos < totdist)
			{
				yield return new WaitForFixedUpdate();

				_position = startgo + ((wohingo - startgo).normalized * pos) - Origin.position;
				UpdateMeshPositions();

				if(pos + 1 > totdist)
					Grappler.rotation = Quaternion.Slerp(Grappler.rotation, q, (float)(1 - totdist + pos));

				pos += speed;
			}

			yield return new WaitForFixedUpdate();

			_position = wohingo - Origin.position;
			UpdateMeshPositions();

			Grappler.rotation = q;

			yield return new WaitForSeconds(1);

			_SetTarget(wohintarget);
		}

		private IEnumerator ComeBack()
		{
			yield return new WaitForSeconds(1);

			Vector3 targetPosition = startgo - Origin.position;
			Vector3 originPosition = wohingo - Origin.position;

			float totdist = (wohingo - startgo).magnitude;

			float speed = 0.005f; // pro Frame...

			float pos = totdist;


			Quaternion q = Grappler.rotation; // das ist's wo wir sind, das brauch ich für den Übergang

			Vector3 wohin = part.transform.position - part.transform.right;

			while(pos > speed)
			{
				yield return new WaitForFixedUpdate();

				_position = (targetPosition - originPosition).normalized * pos;
				UpdateMeshPositions();

				// jetzt rechnen wir den Head-Turn wieder aus
				if(pos + 1 > totdist)
					Grappler.rotation = Quaternion.Slerp(Grappler.rotation, q, (float)(1 - totdist + pos));

				pos -= speed;
			}

			yield return new WaitForFixedUpdate();

			_position = targetPosition;
			UpdateMeshPositions();
			Grappler.rotation = q;



			// jetzt nach 0 zurückdrehen...
// ( FEHLER, eigentlich nur in eine Richtung, die andere muss noch bleiben)

			Vector3 tstart = _position + Origin.position - Origin.position;
Vector3 vv = Grappler.localPosition;
Grappler.localPosition = GrapplerOriginalPosition;
			Vector3 twohin = Grappler.position - Origin.position;
Grappler.localPosition = vv;

			float angle = Vector3.Angle(tstart, twohin);
			Quaternion qangle = Quaternion.FromToRotation(tstart, twohin);

			float a = 0;

			while(a + 1 < angle)
			{
				_position = Quaternion.Slerp(Quaternion.identity, qangle, a / angle) * tstart;
				UpdateMeshPositions();

				a += 1;

				yield return new WaitForFixedUpdate();
			}

			_position = twohin;
			UpdateMeshPositions();

			yield return new WaitForFixedUpdate();

			// jetzt die Rotation noch auf 0 zurückdrehen...



			float a2 = AngleSigned(part.transform.up, Anchor.up, part.transform.right) - 90;

			while(a2 > 90)
				a2 -= 180;
			while(a2 < -90)
				a2 += 180;

			if(a2 < 0)
			{
				while(a2 < -1)
				{
					a2 += 1;

					Anchor.rotation = Quaternion.AngleAxis(90 - a2, part.transform.right) * part.transform.rotation;

					yield return new WaitForFixedUpdate();
				}
			}
			else
			{
				while(a2 > 1)
				{
					a2 -= 1;

					Anchor.rotation = Quaternion.AngleAxis(90 - a2, part.transform.right) * part.transform.rotation;

					yield return new WaitForFixedUpdate();
				}
			}

			Anchor.rotation = Quaternion.AngleAxis(90, part.transform.right) * part.transform.rotation;
		}


		[KSPEvent(name = "ToggleEnforcement", active = false, guiName = "Toggle Enforcement", guiActiveEditor = false)]
		public void ToggleEnforcement()
		{
			if(!IsLinked || !IsConnectionOrigin)
				return;
			IsEnforced = !IsEnforced;
			DestroyJoint();
			if(!IsFreeAttached)
				CreateJoint(part.GetComponent<Rigidbody>(), Target.part.parent.GetComponent<Rigidbody>(), LinkType, Target.transform.position);
			else
			{
			}
			OSD.PostMessage("Joint enforcement temporarily changed.");
			UpdateGui();
		}

		[KSPEvent(name = "ToggleLink", active = false, guiName = "Toggle Link", guiActiveUnfocused = true,
			unfocusedRange = Config.UNFOCUSED_RANGE)]
		public void ToggleLink()
		{
			if(Mode == Mode.Linked)
			{
				if(IsConnectionOrigin)
					Unlink();
				else
				{
					if(Targeter != null)
						Targeter.Unlink();
				}
			}
			else if(Mode == Mode.Unlinked &&
					 ((Target != null && Target.IsConnectionFree) || (Targeter != null && Targeter.IsConnectionFree)))
			{
				if(Target != null)
				{
					if(this.IsPossibleTarget(Target))
					{
						Target.Targeter = this;
						Target.SetAsTarget();
					}
					else
						OSD.PostMessage("Can't relink at the moment, target may be obstructed.");
				}
				else if(Targeter != null)
				{
					if(Targeter.IsPossibleTarget(this))
						SetAsTarget();
					else
						OSD.PostMessage("Can't relink at the moment, targeter may be obstructed.");
				}
			}
			UpdateGui();
		}

		[KSPAction("ToggleLinkAction", KSPActionGroup.None, guiName = "Toggle Link")]
		public void ToggleLinkAction(KSPActionParam param)
		{
			if(Mode == Mode.Linked ||
				(Mode == Mode.Unlinked &&
				 ((Target != null && Target.IsConnectionFree) || (Targeter != null && Targeter.IsConnectionFree))))
			{
				ToggleLink();
			}
		}

		[KSPEvent(name = "UnDock", active = false, guiName = "Undock from Target", guiActiveEditor = false,
			guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
		public void UnDock()
		{
			ProcessUnDock();
		}

		[KSPEvent(name = "Unlink", active = false, guiName = "Unlink", guiActiveEditor = false,
			guiActiveUnfocused = true, unfocusedRange = Config.UNFOCUSED_RANGE)]
		public void Unlink()
		{
			ProcessUnlink(true, false);
		}

		public void LateUpdate()
		{
			if(!strutFinallyCreated)
				return;

			if(Target == null || !IsConnectionOrigin)
				return;

			var refPos = Target.ModelFeatures[ModelFeaturesType.HeadExtension]
					? Target.StrutOrigin.position
					: Target.Origin.position;

//			if((strutFinallyCreated &&
//                 Vector3.Distance(refPos, oldTargetPosition) <= Config.Instance.StrutRealignDistanceTolerance))
//            {
//                return;
//            }


	//		oldTargetPosition = refPos;
	//		DestroyStrut();

if(false) // FEHLER FEHLER temp
{

				var strut = Strut;
				strut.LookAt(refPos);
				strut.Rotate(new Vector3(0, 1, 0), 90f);
				strut.localScale = new Vector3(1, 1, 1);

				var distance = (Vector3.Distance(Vector3.zero, Strut.InverseTransformPoint(refPos)) * 1 *
								StrutScaleFactor); //*-1

		//		if(IsFreeAttached)
		//			distance += Config.Instance.FreeAttachStrutExtension;

				Strut.localScale = new Vector3(distance, 1, 1);

}

		//		TransformLights(true, target, IsDocked);

		//		if(ModelFeatures[ModelFeaturesType.HeadExtension])
		//			headTransform.LookAt(target);
		}

		public void Update()
		{
			if(!initialized)
				return;

			if(delayedStartFlag)
			{
				_delayedStart();
				return;
			}

			if(jointBroken)
			{
				jointBroken = false;
				Unlink();
				return;
			}

			if(IsLinked)
			{
				if(strutRealignCounter > 0 && strutFinallyCreated)
					--strutRealignCounter;
				else
				{
					strutRealignCounter = Config.Instance.StrutRealignInterval;
					UpdateSimpleLights();
					_realignStrut();
					if(IsFreeAttached)
						LinkType = LinkType.Weak;
					else if(IsConnectionOrigin)
					{
						if(Target != null)
						{
							LinkType = Target.IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
						}
					}
					else
					{
						if(Targeter != null)
						{
							LinkType = IsTargetOnly ? LinkType.Normal : LinkType.Maximum;
						}
					}
				}
			}
			else
				LinkType = LinkType.None;

			if(Mode == Mode.Unlinked || Mode == Mode.Target || Mode == Mode.Targeting)
			{
				if(IsTargetOnly)
					_showTargetGrappler(false);
				return;
			}

			if(IsTargetOnly)
			{
				if(!this.AnyTargetersConnected())
				{
					_showTargetGrappler(false);
					Mode = Mode.Unlinked;
					UpdateGui();
					return;
				}
				_showTargetGrappler(true);
			}

			if(Mode == Mode.Linked)
			{
				if(HighLogic.LoadedSceneIsEditor)
					return;

				if(IsOwnVesselConnected)
				{
					if(IsFreeAttached)
					{
						if(FreeAttachPart != null)
						{
							if(FreeAttachPart.vessel != vessel)
								IsOwnVesselConnected = false;
						}
					}
					else if(IsTargetOnly)
					{
						foreach(
							var connectedTargeter in
								this.GetAllConnectedTargeters()
									.Where(
										connectedTargeter =>
											connectedTargeter.vessel != null && connectedTargeter.vessel != vessel))
						{
							connectedTargeter.Unlink();
						}
					}
					else if(Target != null)
					{
						if(Target.vessel != vessel)
							IsOwnVesselConnected = false;
					}

					if(!IsOwnVesselConnected)
						Unlink();

					UpdateGui();
				}
			}
		}

		public void UpdateGui()
		{
			Events["ToggleEnforcement"].active = Events["ToggleEnforcement"].guiActive = false;

			if(HighLogic.LoadedSceneIsEditor || IsTargetOnly || !IsConnectionOrigin || !IsLinked)
				Fields["IsEnforced"].guiActive = false;

			if(HighLogic.LoadedSceneIsFlight)
			{
				if(vessel != null && vessel.isEVA)
				{
					Events["UnDock"].active = Events["UnDock"].guiActive = false;
					Events["Dock"].active = Events["UnDock"].guiActive = false;
					Events["Link"].active = Events["Link"].guiActive = false;
					Events["AbortLink"].active = Events["AbortLink"].guiActive = false;
					Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
					Events["FreeAttach"].active = Events["FreeAttach"].guiActive = false;
					Events["SetAsTarget"].active = Events["SetAsTarget"].guiActive = false;
					Events["FreeAttachStraight"].active = Events["FreeAttachStraight"].guiActive = false;
					return;
				}

				switch(Mode)
				{
				case Mode.Linked:
					Events["Link"].active = Events["Link"].guiActive = false;
					Events["SetAsTarget"].active = Events["SetAsTarget"].guiActive = false;
					Events["FreeAttach"].active = Events["FreeAttach"].guiActive = false;

					if(!IsTargetOnly)
					{
						Events["AbortLink"].active = Events["AbortLink"].guiActive = false;
						if(IsConnectionOrigin)
							Events["ToggleEnforcement"].active = Events["ToggleEnforcement"].guiActive = true;

						Fields["IsEnforced"].guiActive = true;

						if(IsFreeAttached)
						{
							Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
							Events["Unlink"].active = Events["Unlink"].guiActive = true;
						}
						else
						{
							Events["ToggleLink"].active = Events["ToggleLink"].guiActive = true;
							Events["Unlink"].active = Events["Unlink"].guiActive = false;
						}

						if(!IsOwnVesselConnected && !IsDocked)
						{
							if(Config.Instance.DockingEnabled &&
								!(IsFreeAttached
									? FreeAttachPart != null && FreeAttachPart.vessel == vessel
									: Target != null && Target.part != null && Target.part.vessel == vessel))
							{
								Events["Dock"].active = Events["Dock"].guiActive = true;
							}
							Events["UnDock"].active = Events["UnDock"].guiActive = false;
						}

						if(!IsOwnVesselConnected && IsDocked)
						{
							Events["Dock"].active = Events["Dock"].guiActive = false;
							Events["UnDock"].active = Events["UnDock"].guiActive = true;
						}
					}
					else
					{
						Events["Unlink"].active = Events["Unlink"].guiActive = false;
						Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
					}
					break;

				case Mode.Unlinked:
					Events["SetAsTarget"].active = Events["SetAsTarget"].guiActive = false;
					Events["Unlink"].active = Events["Unlink"].guiActive = false;
					Events["UnDock"].active = Events["UnDock"].guiActive = false;
					Events["Dock"].active = Events["Dock"].guiActive = false;

					if(IsTargetOnly)
						Events["Link"].active = Events["Link"].guiActive = false;
					else
					{
						Events["Link"].active = Events["Link"].guiActive = true;
						Events["FreeAttach"].active = Events["FreeAttach"].guiActive = true;
						Events["AbortLink"].active = Events["AbortLink"].guiActive = false;
						if((Target != null && Target.IsConnectionFree) || (Targeter != null && Targeter.IsConnectionFree))
							Events["ToggleLink"].active = Events["ToggleLink"].guiActive = true;
						else
							Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
					}
					break;

				case Mode.Target:
					Events["UnDock"].active = Events["UnDock"].guiActive = false;
					Events["Dock"].active = Events["Dock"].guiActive = false;
					Events["SetAsTarget"].active = Events["SetAsTarget"].guiActive = true;
					if(!IsTargetOnly)
						Events["Link"].active = Events["Link"].guiActive = false;
					Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
					Events["FreeAttach"].active = Events["FreeAttach"].guiActive = false;
					break;

				case Mode.Targeting:
					Events["UnDock"].active = Events["UnDock"].guiActive = false;
					Events["Dock"].active = Events["Dock"].guiActive = false;
					Events["Link"].active = Events["Link"].guiActive = false;
					Events["AbortLink"].active = Events["AbortLink"].guiActive = true;
					Events["ToggleLink"].active = Events["ToggleLink"].guiActive = false;
					Events["FreeAttach"].active = Events["FreeAttach"].guiActive = false;
					break;
				}
				Events["FreeAttachStraight"].active =
					Events["FreeAttachStraight"].guiActive = Events["FreeAttach"].active;
			}
			else if(HighLogic.LoadedSceneIsEditor)
			{
				Events["ToggleLink"].active =
					Events["ToggleLink"].guiActive = Events["ToggleLink"].guiActiveEditor = false;
				Events["UnDock"].active = Events["UnDock"].guiActive = Events["UnDock"].guiActiveEditor = false;
				Events["Dock"].active = Events["Dock"].guiActive = Events["Dock"].guiActiveEditor = false;

				switch(Mode)
				{
				case Mode.Linked:
					if(!IsTargetOnly)
						Events["Unlink"].active =
							Events["Unlink"].guiActive = Events["Unlink"].guiActiveEditor = true;
					Events["Link"].active = Events["Link"].guiActive = Events["Link"].guiActiveEditor = false;
					Events["SetAsTarget"].active =
						Events["SetAsTarget"].guiActive = Events["SetAsTarget"].guiActiveEditor = false;
					Events["AbortLink"].active =
						Events["AbortLink"].guiActive = Events["AbortLink"].guiActiveEditor = false;
					Events["FreeAttach"].active =
						Events["FreeAttach"].guiActive = Events["FreeAttach"].guiActiveEditor = false;
					break;

				case Mode.Unlinked:
					Events["Unlink"].active = Events["Unlink"].guiActive = Events["Unlink"].guiActiveEditor = false;
					Events["SetAsTarget"].active =
						Events["SetAsTarget"].guiActive = Events["SetAsTarget"].guiActiveEditor = false;
					Events["AbortLink"].active =
						Events["AbortLink"].guiActive = Events["AbortLink"].guiActiveEditor = false;
					if(!IsTargetOnly)
					{
						Events["Link"].active = Events["Link"].guiActive = Events["Link"].guiActiveEditor = true;
						Events["FreeAttach"].active =
							Events["FreeAttach"].guiActive = Events["FreeAttach"].guiActiveEditor = true;
					}
					break;

				case Mode.Target:
					Events["Unlink"].active = Events["Unlink"].guiActive = Events["Unlink"].guiActiveEditor = false;
					Events["Link"].active = Events["Link"].guiActive = Events["Link"].guiActiveEditor = false;
					Events["SetAsTarget"].active =
						Events["SetAsTarget"].guiActive = Events["SetAsTarget"].guiActiveEditor = true;
					Events["AbortLink"].active =
						Events["AbortLink"].guiActive = Events["AbortLink"].guiActiveEditor = false;
					Events["FreeAttach"].active =
						Events["FreeAttach"].guiActive = Events["FreeAttach"].guiActiveEditor = false;
					break;

				case Mode.Targeting:
					Events["Unlink"].active = Events["Unlink"].guiActive = Events["Unlink"].guiActiveEditor = false;
					Events["Link"].active = Events["Link"].guiActive = Events["Link"].guiActiveEditor = false;
					Events["SetAsTarget"].active =
						Events["SetAsTarget"].guiActive = Events["SetAsTarget"].guiActiveEditor = false;
					Events["AbortLink"].active =
						Events["AbortLink"].guiActive = Events["AbortLink"].guiActiveEditor = true;
					Events["FreeAttach"].active =
						Events["FreeAttach"].guiActive = Events["FreeAttach"].guiActiveEditor = false;
					break;
				}
				Events["FreeAttachStraight"].active =
					Events["FreeAttachStraight"].guiActive =
						Events["FreeAttachStraight"].guiActiveEditor = Events["FreeAttach"].active;

				if(!Config.Instance.AllowFreeAttachInEditor)
					Events["FreeAttach"].guiActiveEditor = false;
			}
		}

		private void _continueWithStraightOutAttach(Part newPart)
		{
			var rayres = _performStraightOutRaycast();
			if(rayres.Item1)
			{
				ActiveStrutsAddon.NewSpawnedPart = newPart;
				ActiveStrutsAddon.CurrentTargeter = this;
				StartCoroutine(ActiveStrutsAddon.PlaceNewPart(rayres.Item2.PartFromHit(), rayres.Item2));
				return;
			}
			OSD.PostMessage("Straight Out Attach failed!");
			Debug.Log("[IRAS] straight out raycast didn't hit anything after part creation");
			DestroyImmediate(newPart);
		}

		private void _delayedStart()
		{
			if(ticksForDelayedStart > 0)
			{
				ticksForDelayedStart--;
				return;
			}
			delayedStartFlag = false;
			if(Id == Guid.Empty.ToString())
				Id = Guid.NewGuid().ToString();
			if(HighLogic.LoadedSceneIsFlight && !IdResetDone)
				ActiveStrutsAddon.Enqueue(this);
			if(IsLinked)
			{
				if(IsTargetOnly)
					Mode = Mode.Linked;
				else
					Reconnect();
			}
			else
				Mode = Mode.Unlinked;
			Events.Sort((l, r) =>
			{
				if(l.name == "Link" && r.name == "FreeAttach")
					return -1;
				if(r.name == "Link" && l.name == "FreeAttach")
					return 1;
				if(l.name == "FreeAttach" && r.name == "FreeAttachStraight")
					return -1;
				if(r.name == "FreeAttach" && l.name == "FreeAttachStraight")
					return 1;
				if(l.name == "Link" && r.name == "FreeAttachStraight")
					return -1;
				if(r.name == "Link" && l.name == "FreeAttachStraight")
					return 1;
				if(r.name == "ToggleEnforcement")
					return 1;
				if(l.name == "ToggleEnforcement")
					return -1;
				return string.Compare(l.name, r.name, StringComparison.Ordinal);
			}
				);
			UpdateGui();
		}

		private Tuple<bool, RaycastHit> _performStraightOutRaycast()
		{
			var rayRes = Utilities.PerformRaycastIntoDir(Origin.position, RealModelForward, RealModelForward, part);
			return new Tuple<bool, RaycastHit>(rayRes.HitResult, rayRes.Hit);
		}

		private void _realignStrut()
		{
			return; // ich will das mal nicht mehr realignen... weil das Müll ist... sag ich

			if(IsFreeAttached)
			{
				lock(freeAttachStrutUpdateLock)
				{
					Vector3[] targetPos;
					if(StraightOutAttachAppliedInEditor || straightOutAttached)
					{
						var raycast = _performStraightOutRaycast();
						if(!raycast.Item1)
						{
							DestroyStrut();
							IsLinked = false;
							IsFreeAttached = false;
							return;
						}
						targetPos = new[] {raycast.Item2.point, raycast.Item2.normal};
					}
					else
					{
						var raycast = Utilities.PerformRaycast(Origin.position, FreeAttachPart.transform.position,
							Origin.up, new[] {FreeAttachPart, part});
						targetPos = new[] {FreeAttachPart.transform.position, raycast.Hit.normal};
					}

					if(strutFinallyCreated && 
						(Vector3.Distance(targetPos[0], oldTargetPosition) <=
						 Config.Instance.StrutRealignDistanceTolerance))
						return;
					oldTargetPosition = targetPos[0];
					DestroyStrut();
					CreateStrut(targetPos[0]);
					ShowGrappler(true, targetPos[0], targetPos[0], false, targetPos[1], true);
				}
			}
			else if(!IsTargetOnly)
			{
				if(Target == null || !IsConnectionOrigin)
					return;

				var refPos = Target.ModelFeatures[ModelFeaturesType.HeadExtension]
						? Target.StrutOrigin.position
						: Target.Origin.position;

				if((strutFinallyCreated &&
					 Vector3.Distance(refPos, oldTargetPosition) <= Config.Instance.StrutRealignDistanceTolerance))
				{
					return;
				}
				oldTargetPosition = refPos;
				DestroyStrut();

				if(Target.IsTargetOnly)
				{
					CreateStrut(Target.ModelFeatures[ModelFeaturesType.HeadExtension]
						? Target.StrutOrigin.position
						: Target.Origin.position);
					ShowGrappler(false, Vector3.zero, Vector3.zero, false, Vector3.zero);
				}
				else
				{
					var targetStrutPos = Target.ModelFeatures[ModelFeaturesType.HeadExtension]
						? Target.StrutOrigin.position
						: Target.Origin.position;
					var localStrutPos = ModelFeatures[ModelFeaturesType.HeadExtension]
						? StrutOrigin.position
						: Origin.position;
					Target.DestroyStrut();
					CreateStrut(targetStrutPos, 0.5f, -1*GrapplerOffset);
					Target.CreateStrut(localStrutPos, 0.5f, -1*GrapplerOffset);
					var grapplerTargetPos = ((targetStrutPos - localStrutPos)*0.5f) + localStrutPos;
					ShowGrappler(true, grapplerTargetPos, targetStrutPos, true, Vector3.zero);
					Target.ShowGrappler(true, grapplerTargetPos, localStrutPos, true, Vector3.zero);
				}
			}
		}

		private void _showTargetGrappler(bool show)
		{
			if(!IsTargetOnly || !ModelFeatures[ModelFeaturesType.Grappler])
				return;
			if(show && !targetGrapplerVisible)
			{
				Grappler.Translate(new Vector3(-GrapplerOffset, 0, 0));
				targetGrapplerVisible = true;
			}
			else if(!show && targetGrapplerVisible)
			{
				Grappler.Translate(new Vector3(GrapplerOffset, 0, 0));
				targetGrapplerVisible = false;
			}
		}

		private void TransformLights(bool show, Vector3 lookAtTarget, bool bright = false)
		{
			if(LightsDull == null || LightsBright == null || ModelFeatures == null)
				return;
			
			if(!(ModelFeatures[ModelFeaturesType.LightsBright] && ModelFeatures[ModelFeaturesType.LightsDull]))
				return;

			if(!show)
			{
				LightsBright.localScale = Vector3.zero;
				LightsDull.localScale = Vector3.zero;
				if(dullLightsExtended)
				{
					LightsDull.Translate(new Vector3(LightsOffset, 0, 0));
					dullLightsExtended = false;
				}
				if(brightLightsExtended)
				{
					LightsBright.Translate(new Vector3(LightsOffset, 0, 0));
					brightLightsExtended = false;
				}
				return;
			}

			if(bright)
			{
				LightsDull.localScale = Vector3.zero;
				LightsBright.LookAt(lookAtTarget);
				LightsBright.Rotate(new Vector3(0, 1, 0), 90f);
				LightsBright.localScale = new Vector3(1, 1, 1);
				if(!brightLightsExtended)
					LightsBright.Translate(new Vector3(-LightsOffset, 0, 0));
				if(dullLightsExtended)
					LightsDull.Translate(new Vector3(LightsOffset, 0, 0));
				dullLightsExtended = false;
				brightLightsExtended = true;
				return;
			}

			LightsBright.localScale = Vector3.zero;
			LightsDull.LookAt(lookAtTarget);
			LightsDull.Rotate(new Vector3(0, 1, 0), 90f);
			LightsDull.position = Origin.position;
			LightsDull.localScale = new Vector3(1, 1, 1);

			if(!dullLightsExtended)
				LightsDull.Translate(new Vector3(-LightsOffset, 0, 0));
			if(brightLightsExtended)
				LightsBright.Translate(new Vector3(LightsOffset, 0, 0));
			
			dullLightsExtended = true;
			brightLightsExtended = false;
		}

		private void UpdateSimpleLights()
		{
			try
			{
				if(!ModelFeatures[ModelFeaturesType.SimpleLights])
					return;
			}
			catch (KeyNotFoundException)
			{ return; }
			catch (NullReferenceException)
			{ return; }

			Color col;
			if(IsLinked)
				col = Utilities.SetColorForEmissive(IsDocked ? Color.blue : Color.green);
			else
				col = Utilities.SetColorForEmissive(Color.yellow);

			foreach(
				var m in
					new[] {simpleLights, simpleLightsSecondary}.Select(
					lightTransform => lightTransform.GetComponent<Renderer>().material))
			{
				m.SetColor("_Emissive", col);
				m.SetColor("_MainTex", col);
				m.SetColor("_EmissiveColor", col);
			}
		}

		internal enum ModelFeaturesType
		{
			Grappler,
			LightsBright,
			LightsDull,
			SimpleLights,
			HeadExtension,
			Animation
		}

		private class OrientationInfo
		{
			internal OrientationInfo(string stringToParse)
			{
				if(string.IsNullOrEmpty(stringToParse))
				{
					Orientation = Orientations.Up;
					Invert = false;
					return;
				}
				var substrings = stringToParse.Split(',').Select(s => s.Trim().ToUpperInvariant()).ToList();
				if(substrings.Count == 2)
				{
					var oS = substrings[0];
					if(oS == "RIGHT")
						Orientation = Orientations.Right;
					else if(oS == "FORWARD")
						Orientation = Orientations.Forward;
					else
						Orientation = Orientations.Up;
					bool outBool;
					bool.TryParse(substrings[1], out outBool);
					Invert = outBool;
				}
			}

			internal OrientationInfo(Orientations orientation, bool invert)
			{
				Orientation = orientation;
				Invert = invert;
			}

			private bool Invert { get; set; }
			private Orientations Orientation { get; set; }

			internal Vector3 GetAxis(Transform transform)
			{
				var axis = Vector3.zero;
				if(transform == null)
					return axis;
				switch(Orientation)
				{
				case Orientations.Forward:
					axis = transform.forward;
					break;
				case Orientations.Right:
					axis = transform.right;
					break;
				case Orientations.Up:
					axis = transform.up;
					break;
				}
				axis = Invert ? axis*-1f : axis;
				return axis;
			}
		}

		private enum Orientations
		{
			Up,
			Forward,
			Right
		}

//		altes Zeugs, das ich rausgeschmissen habe, weil das Mod was anderes werden soll

//        private void MoveFakeRopeSling(bool local, GameObject sling)
//        {
//            sling.SetActive(false);
//            var trans = sling.transform;
//            trans.rotation = local ? Origin.rotation : Target.Origin.rotation;
//            trans.Rotate(new Vector3(0, 0, 1), 90f);
//            trans.Rotate(new Vector3(1, 0, 0), 90f);
//            trans.LookAt(local ? Target.FlexOffsetOriginPosition : FlexOffsetOriginPosition);
//            var dir = Target.FlexOffsetOriginPosition - FlexOffsetOriginPosition;
//            if(!local)
//                dir *= -1;
//            trans.position = (local ? FlexOffsetOriginPosition : Target.FlexOffsetOriginPosition) +
//                             (dir.normalized*FlexibleStrutSlingOffset);
//            sling.SetActive(true);
//        }

	}

	//CModuleStrut : CompountPartModule

	//das hat mehrere
	//    CompoundPart drin

	//das wiederum hat ein
	//    CompoundPartModule drin
	//und ein
	//    ModuleLinkedMesh : CompoundPartModule

	public abstract class aCompoundPartModule : PartModule
	{
		private aCompoundPart _compoundPart;

		public aCompoundPart compoundPart
		{
			get { return _compoundPart; }
			set { _compoundPart = value; }
		}

		public Part target
		{
			get { return _compoundPart.target; }
		}

		public sealed override void OnAwake()
		{
			this._compoundPart = (base.part as aCompoundPart);
			if (this._compoundPart == null)
			{
				Debug.LogError("[CompoundPartModule]: CompoundPartModule requires a CompoundPart component!", base.gameObject);
			}
			this.OnModuleAwake();
		}

		protected virtual void OnModuleAwake()
		{}

		public abstract void OnTargetSet(Part target);
		public abstract void OnTargetLost();

		public virtual void OnPreviewAttachment(Vector3 rDir, Vector3 rPos, Quaternion rRot)
		{}

		public virtual void OnPreviewEnd()
		{}

		public virtual void OnTargetUpdate()
		{}
	}

	public class aCompoundPart : Part
	{
		public enum AttachState
		{
			Detached,
			Attaching,
			Attached
		}

		public Vector3 direction;

		public Vector3 targetPosition;

		public Quaternion targetRotation;

		public Part target;

//		private CModuleLinkedMesh linkedMesh;

		public float maxLength = 10f;

		public aCompoundPart.AttachState attachState;

		private RaycastHit hit;

		private bool hasSaveData;

		private uint tgtId;

		private bool needsDirectionFlip;

		private aCompoundPart original;

		private aCompoundPartModule[] cmpModules;

		private Vector3 wTgtPos;

		private Quaternion wTgtRot;

		private bool tweakEnded = true;

//		public bool isTweakingTarget
//		{
//			get
//			{
//				return this.linkedMesh.tweakingTarget;
//			}
//		}

		protected override void onCopy(Part original, bool asSymCPart)
		{
			this.hasSaveData = false;
			if (this.symmetryCounterparts.Contains(EditorLogic.SelectedPart))
			{
				this.direction = Vector3.zero;
			}
			else if (asSymCPart && EditorLogic.fetch.symmetryMethod == SymmetryMethod.Mirror)
			{
				this.original = (aCompoundPart)original;
				this.needsDirectionFlip = true;
			}
		}

		protected override void onPartAwake()
		{
//			this.hasSaveData = false;
//			this.cmpModules = base.GetComponents<aCompoundPartModule>();
//          List<ICompoundPartAnchorMethod> list = new List<ICompoundPartAnchorMethod>();
//          int num = this.cmpModules.Length;
//          for (int i = 0; i < num; i++)
//          {
//				ICompoundPartAnchorMethod compoundPartAnchorMethod = this.cmpModules[i] as ICompoundPartAnchorMethod;
//				if (compoundPartAnchorMethod != null)
//				{
//					list.Add(compoundPartAnchorMethod);
//				}
//              CModuleLinkedMesh component = this.cmpModules[i].GetComponent<CModuleLinkedMesh>();
//              if (component != null)
//              {
//                  this.linkedMesh = component;
//              }
//          }
//          GameEvents.onEditorPartEvent.Add(new EventData<ConstructionEventType, Part>.OnEvent(this.OnEditorEvent));
//          this.compund = true;
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("tgt", this.tgtId);
			node.AddValue("pos", KSPUtil.WriteVector(this.targetPosition));
			node.AddValue("rot", KSPUtil.WriteQuaternion(this.targetRotation));
			node.AddValue("dir", KSPUtil.WriteVector(this.direction));
		}

		public override void OnLoad(ConfigNode node)
		{
			this.hasSaveData = node.HasData;
			if (node.HasValue("tgt"))
			{
				this.tgtId = uint.Parse(node.GetValue("tgt"));
			}
			if (node.HasValue("pos"))
			{
				this.targetPosition = KSPUtil.ParseVector3(node.GetValue("pos"));
			}
			if (node.HasValue("dir"))
			{
				this.direction = KSPUtil.ParseVector3(node.GetValue("dir"));
			}
			if (node.HasValue("rot"))
			{
				this.targetRotation = KSPUtil.ParseQuaternion(node.GetValue("rot"));
			}
		}

		protected override void onStartComplete()
		{
			if (this.customPartData != string.Empty)
			{
				this.OnLoad(this.ParseCustomPartData(this.customPartData));
				this.customPartData = string.Empty;
			}
			this.attachState = aCompoundPart.AttachState.Detached;
			this.target = null;
			Part exists = null;
			if (this.hasSaveData)
			{
				if (HighLogic.LoadedSceneIsFlight)
				{
					exists = this.vessel.parts.Find((Part p) => p.craftID == this.tgtId && p.missionID == this.missionID);
				}
				if (HighLogic.LoadedSceneIsEditor)
				{
					exists = EditorLogic.fetch.ship.parts.Find((Part p) => p.craftID == this.tgtId);
				}
				if (exists)
				{
					this.SetTarget(exists);
				}
				else if (HighLogic.LoadedSceneIsEditor && this.direction != Vector3.zero)
				{
					Debug.LogWarning(string.Concat(new string[]
					{
						"[CompoundPart]: No target found with craftID ",
						this.craftID.ToString(),
						". Attempting to find it at direction [",
						KSPUtil.WriteVector(this.direction),
						"]."
					}), base.gameObject);
					base.StartCoroutine(CallbackUtil.DelayedCallback(1, new Callback(this.schedule_raycast)));
				}
			}
		}

		protected override void onPartAttach(Part parent)
		{
			if (EditorLogic.SelectedPart == this)
			{
				this.lockEditor();
				this.attachState = aCompoundPart.AttachState.Attaching;
			}
			else
			{
				this.attachState = aCompoundPart.AttachState.Detached;
				if (this.direction != Vector3.zero)
				{
					if (this.needsDirectionFlip)
					{
						Vector3 vector = this.original.transform.TransformDirection(this.original.direction);
						vector = new Vector3(-vector.x, vector.y, vector.z);
						this.direction = base.transform.InverseTransformDirection(vector);
						this.needsDirectionFlip = false;
					}
					base.StartCoroutine(CallbackUtil.DelayedCallback(1, new Callback(this.schedule_raycast)));
				}
			}
		}

		protected override void onPartDetach()
		{
			if (EditorLogic.SelectedPart == this || (this.target && this.target.localRoot != base.localRoot))
			{
				this.DumpTarget();
				this.attachState = aCompoundPart.AttachState.Detached;
				if (EditorLogic.SelectedPart == this)
				{
					this.direction = Vector3.zero;
				}
			}
		}

		protected override void onPartDestroy()
		{
			this.DumpTarget();
			this.unlockEditor();
			GameEvents.onEditorPartEvent.Remove(new EventData<ConstructionEventType, Part>.OnEvent(this.OnEditorEvent));
		}

		public override void onEditorStartTweak()
		{
//			if (this.tweakEnded)
//			{
//				float num = Vector3.Distance(Input.mousePosition, EditorLogic.fetch.editorCamera.WorldToScreenPoint(base.transform.position));
//				float num2 = Vector3.Distance(Input.mousePosition, EditorLogic.fetch.editorCamera.WorldToScreenPoint(this.linkedMesh.targetAnchor.position));
//				this.linkedMesh.tweakingTarget = (num2 < num);
//				this.tweakEnded = false;
//			}
		}

		public override void onEditorEndTweak()
		{
//			if (this.linkedMesh != null)
//			{
//				this.linkedMesh.tweakingTarget = false;
//			}
//			this.tweakEnded = true;
		}

		public override Transform GetReferenceTransform()
		{
//			Transform result;
//			if (this.linkedMesh.targetCollider == null)
//			{
//				result = base.transform;
//			}
//			else
//			{
//				result = ((!this.linkedMesh.tweakingTarget) ? base.transform : this.linkedMesh.targetAnchor);
//			}
//			return result;
return null;
		}

		public override Collider[] GetPartColliders()
		{
//			Transform transform = (!this.linkedMesh.tweakingTarget) ? this.linkedMesh.mainAnchor : this.linkedMesh.targetAnchor;
//			return transform.GetComponentsInChildren<Collider>();
return new Collider[1];
		}

		private void onTargetDetach()
		{
			if (!this)
			{
				return;
			}
			this.UnsetLink();
			this.attachState = aCompoundPart.AttachState.Detached;
		}

		private void onTargetDestroy()
		{
			if (!this)
			{
				return;
			}
			this.UnsetLink();
			this.attachState = aCompoundPart.AttachState.Detached;
		}

		private void onTargetReattach()
		{
			if (!this)
			{
				return;
			}
			base.StartCoroutine(CallbackUtil.DelayedCallback(1, new Callback(this.schedule_raycast)));
			if (EditorLogic.fetch.symmetryMethod == SymmetryMethod.Radial)
			{
				for (int i = 0; i < this.symmetryCounterparts.Count; i++)
				{
					aCompoundPart compoundPart = (aCompoundPart)this.symmetryCounterparts[i];
					compoundPart.direction = this.direction;
					compoundPart.StartCoroutine(CallbackUtil.DelayedCallback(1, new Callback(compoundPart.schedule_raycast)));
				}
			}
			else if (EditorLogic.fetch.symmetryMethod == SymmetryMethod.Mirror)
			{
				Vector3 vector = base.transform.TransformDirection(this.direction);
				vector = new Vector3(-vector.x, vector.y, vector.z);
				for (int j = 0; j < this.symmetryCounterparts.Count; j++)
				{
					aCompoundPart compoundPart2 = (aCompoundPart)this.symmetryCounterparts[j];
					compoundPart2.direction = compoundPart2.transform.InverseTransformDirection(vector);
					compoundPart2.StartCoroutine(CallbackUtil.DelayedCallback(1, new Callback(compoundPart2.schedule_raycast)));
				}
			}
		}

		protected override void onEditorUpdate()
		{
			aCompoundPart.AttachState attachState = this.attachState;
			if (attachState == aCompoundPart.AttachState.Attaching)
			{
				this.onAttachUpdate();
			}
		}

		public override void LateUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight)
			{
				return;
			}
			if (this.attachState == aCompoundPart.AttachState.Attached)
			{
				int num = this.cmpModules.Length;
				while (num-- > 0)
				{
					this.cmpModules[num].OnTargetUpdate();
				}
			}
		}

		protected override void onPartFixedUpdate()
		{
			if (this.attachState == aCompoundPart.AttachState.Attached && (this.target == null || this.target.vessel != this.vessel))
			{
				this.DumpTarget();
			}
		}

		private void onAttachUpdate()
		{
			if (this.direction != Vector3.zero)
			{
				this.targetPosition = base.transform.InverseTransformPoint(this.hit.point);
				this.targetRotation = Quaternion.FromToRotation(Vector3.right, base.transform.InverseTransformDirection(this.hit.normal));
				this.PreviewAttachment(this.direction, this.targetPosition, this.targetRotation);
				if (Input.GetMouseButtonUp(0) && Vector3.Distance(base.transform.position, this.hit.point) <= this.maxLength)
				{
					this.EndPreview();
					this.raycastTarget(this.direction);
					if (this.symMethod == SymmetryMethod.Radial)
					{
						for (int i = 0; i < this.symmetryCounterparts.Count; i++)
						{
							aCompoundPart compoundPart = (aCompoundPart)this.symmetryCounterparts[i];
							compoundPart.raycastTarget(this.direction);
						}
					}
					else if (this.symMethod == SymmetryMethod.Mirror)
					{
						Vector3 vector = base.transform.TransformDirection(this.direction);
						vector = new Vector3(-vector.x, vector.y, vector.z);
						for (int j = 0; j < this.symmetryCounterparts.Count; j++)
						{
							aCompoundPart compoundPart2 = (aCompoundPart)this.symmetryCounterparts[j];
							compoundPart2.raycastTarget(compoundPart2.transform.InverseTransformDirection(vector));
						}
					}
					if (this.target)
					{
						EditorLogic.fetch.ResetBackup();
						EditorLogic.fetch.GetComponent<AudioSource>().PlayOneShot(EditorLogic.fetch.attachClip);
						base.StartCoroutine(CallbackUtil.DelayedCallback(1, new Callback(this.unlockEditor)));
						return;
					}
				}
			}
			this.direction = this.findTargetDirection();
			if (Input.GetKeyDown(KeyCode.Delete))
			{
				this.DumpTarget();
				for (int k = 0; k < this.symmetryCounterparts.Count; k++)
				{
					aCompoundPart compoundPart3 = (aCompoundPart)this.symmetryCounterparts[k];
					compoundPart3.DumpTarget();
				}
				this.unlockEditor();
			}
		}

		private Vector3 findTargetDirection()
		{
			if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out this.hit, 1000f, LayerUtil.DefaultEquivalent))
			{
				Part componentUpwards = this.hit.collider.gameObject.GetComponentUpwards<Part>();
				if (componentUpwards != null)
				{
					return base.transform.InverseTransformPoint(this.hit.point).normalized;
				}
			}
			return Vector3.zero;
		}

		private void lockEditor()
		{
			InputLockManager.SetControlLock(ControlTypes.EDITOR_ICON_HOVER | ControlTypes.EDITOR_ICON_PICK | ControlTypes.EDITOR_TAB_SWITCH | ControlTypes.EDITOR_PAD_PICK_PLACE | ControlTypes.EDITOR_PAD_PICK_COPY | ControlTypes.EDITOR_GIZMO_TOOLS | ControlTypes.EDITOR_ROOT_REFLOW | ControlTypes.EDITOR_SYM_SNAP_UI | ControlTypes.EDITOR_EDIT_STAGES | ControlTypes.EDITOR_UNDO_REDO | ControlTypes.EDITOR_MODE_SWITCH, "aCompoundPart_Placement");
		}

		private void unlockEditor()
		{
			InputLockManager.RemoveControlLock("aCompoundPart_Placement");
		}

		private void schedule_raycast()
		{
			this.raycastTarget(this.direction);
		}

		public bool raycastTarget(Vector3 dir)
		{
			this.direction = dir;
			bool result = false;
			Debug.DrawRay(base.transform.position, base.transform.TransformDirection(dir), Color.yellow, 3f);
			int layer = base.gameObject.layer;
			base.gameObject.SetLayerRecursive(2, 0);
			if (Physics.Raycast(base.transform.position, base.transform.TransformDirection(dir), out this.hit, this.maxLength, EditorLogic.LayerMask))
			{
				Part componentUpwards = this.hit.collider.gameObject.GetComponentUpwards<Part>();
				if (componentUpwards != null && !componentUpwards.frozen)
				{
					this.targetPosition = base.transform.InverseTransformPoint(this.hit.point);
					this.targetRotation = Quaternion.FromToRotation(Vector3.right, base.transform.InverseTransformDirection(this.hit.normal));
					result = true;
					this.SetTarget(componentUpwards);
				}
			}
			base.gameObject.SetLayerRecursive(layer, 0);
			return result;
		}

		private bool SetTarget(Part tgt)
		{
			if (this.target != null)
			{
				this.DumpTarget();
			}
			this.target = tgt;
			if (!(this.target != null))
			{
				return false;
			}
			if (this.target.frozen)
			{
				this.target = null;
				return false;
			}
			Part expr_4E = this.target;
			expr_4E.OnEditorDetach = (Callback)Delegate.Combine(expr_4E.OnEditorDetach, new Callback(this.onTargetDetach));
			Part expr_75 = this.target;
			expr_75.OnEditorDestroy = (Callback)Delegate.Combine(expr_75.OnEditorDestroy, new Callback(this.onTargetDestroy));
			Part expr_9C = this.target;
			expr_9C.OnEditorAttach = (Callback)Delegate.Combine(expr_9C.OnEditorAttach, new Callback(this.onTargetReattach));
			this.tgtId = this.target.craftID;
			this.UpdateWorldValues();
			this.SetLink();
			this.attachState = aCompoundPart.AttachState.Attached;
			return true;
		}

		public void UpdateWorldValues()
		{
			this.wTgtPos = this.target.transform.InverseTransformPoint(base.transform.TransformPoint(this.targetPosition));
			this.wTgtRot = Quaternion.Inverse(this.target.transform.rotation) * base.transform.rotation * this.targetRotation;
		}

		private void DumpTarget()
		{
			if (this.target != null)
			{
				Part expr_17 = this.target;
				expr_17.OnEditorDetach = (Callback)Delegate.Remove(expr_17.OnEditorDetach, new Callback(this.onTargetDetach));
				Part expr_3E = this.target;
				expr_3E.OnEditorDestroy = (Callback)Delegate.Remove(expr_3E.OnEditorDestroy, new Callback(this.onTargetDestroy));
				Part expr_65 = this.target;
				expr_65.OnEditorAttach = (Callback)Delegate.Remove(expr_65.OnEditorAttach, new Callback(this.onTargetReattach));
			}
			this.onEditorEndTweak();
			this.wTgtPos = Vector3.zero;
			this.wTgtRot = Quaternion.identity;
			this.UnsetLink();
			this.target = null;
			this.tgtId = 0u;
			this.attachState = aCompoundPart.AttachState.Detached;
		}

		private void SetLink()
		{
			int num = this.cmpModules.Length;
			while (num-- > 0)
			{
				this.cmpModules[num].OnTargetSet(this.target);
			}
		}

		private void UnsetLink()
		{
			int num = this.cmpModules.Length;
			while (num-- > 0)
			{
				this.cmpModules[num].OnTargetLost();
			}
		}

		private void PreviewAttachment(Vector3 rDir, Vector3 rPos, Quaternion rRot)
		{
			int num = this.cmpModules.Length;
			while (num-- > 0)
			{
				this.cmpModules[num].OnPreviewAttachment(rDir, rPos, rRot);
			}
			if (this.symMethod == SymmetryMethod.Radial)
			{
				for (int i = 0; i < this.symmetryCounterparts.Count; i++)
				{
					aCompoundPart compoundPart = (aCompoundPart)this.symmetryCounterparts[i];
					int num2 = compoundPart.cmpModules.Length;
					while (num2-- > 0)
					{
						compoundPart.cmpModules[num2].OnPreviewAttachment(rDir, rPos, rRot);
					}
				}
			}
			if (this.symMethod == SymmetryMethod.Mirror)
			{
				Vector3 vector = base.transform.TransformDirection(this.direction);
				vector = new Vector3(-vector.x, vector.y, vector.z);
				for (int j = 0; j < this.symmetryCounterparts.Count; j++)
				{
					aCompoundPart compoundPart2 = (aCompoundPart)this.symmetryCounterparts[j];
					Vector3 vector2 = compoundPart2.transform.InverseTransformDirection(vector);
					Vector3 rPos2 = vector2 * rPos.magnitude;
					Quaternion anchorRot = compoundPart2.getAnchorRot(vector2, compoundPart2.targetRotation);
					int num3 = compoundPart2.cmpModules.Length;
					while (num3-- > 0)
					{
						compoundPart2.cmpModules[num3].OnPreviewAttachment(vector2, rPos2, anchorRot);
					}
				}
			}
		}

		private void EndPreview()
		{
			int num = this.cmpModules.Length;
			while (num-- > 0)
			{
				this.cmpModules[num].OnPreviewEnd();
			}
			for (int i = 0; i < this.symmetryCounterparts.Count; i++)
			{
				aCompoundPart compoundPart = (aCompoundPart)this.symmetryCounterparts[i];
				int num2 = compoundPart.cmpModules.Length;
				while (num2-- > 0)
				{
					compoundPart.cmpModules[num2].OnPreviewEnd();
				}
			}
		}

		private Quaternion getAnchorRot(Vector3 rDir, Quaternion defaultRot)
		{
			int layer = base.gameObject.layer;
			base.gameObject.SetLayerRecursive(2, 0);
			Quaternion result = defaultRot;
			if (Physics.Raycast(base.transform.position, base.transform.TransformDirection(rDir), out this.hit, this.maxLength, EditorLogic.LayerMask))
			{
				Part componentUpwards = this.hit.collider.gameObject.GetComponentUpwards<Part>();
				if (componentUpwards != null && !componentUpwards.frozen)
				{
					result = Quaternion.FromToRotation(Vector3.right, base.transform.InverseTransformDirection(this.hit.normal));
				}
			}
			base.gameObject.SetLayerRecursive(layer, 0);
			return result;
		}

		private void OnEditorEvent(ConstructionEventType evt, Part selPart)
		{
			switch (evt)
			{
			case ConstructionEventType.PartOffsetting:
			case ConstructionEventType.PartOffset:
			case ConstructionEventType.PartRotating:
			case ConstructionEventType.PartRotated:
				this.updateTargetCoords();
				break;
			}
		}

		private void updateTargetCoords()
		{
			if (this.target != null)
			{
				this.targetPosition = base.transform.InverseTransformPoint(this.target.transform.TransformPoint(this.wTgtPos));
				this.targetRotation = Quaternion.Inverse(base.transform.rotation) * this.target.transform.rotation * this.wTgtRot;
				this.direction = this.targetPosition.normalized;
				if (this.attachState == aCompoundPart.AttachState.Attached)
				{
					int num = this.cmpModules.Length;
					while (num-- > 0)
					{
						this.cmpModules[num].OnTargetUpdate();
					}
				}
			}
		}

		public ConfigNode ParseCustomPartData(string customPartData)
		{
			ConfigNode configNode = new ConfigNode();
			if (customPartData != string.Empty)
			{
				Debug.LogWarning("[aCompoundPart]: Deprecated 'customPartData' field found. Upgrading to new format...");
				string[] array = customPartData.Split(new char[]
				{
					';'
				});
				string[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					string text = array2[i];
					if (text.Contains(":"))
					{
						string text2 = text.Split(new char[]
						{
							':'
						})[0].Trim();
						string text3 = text.Split(new char[]
						{
							':'
						})[1].Trim();
						string text4 = text2;
						switch (text4)
						{
						case "tgt":
							if (text3.Contains("_"))
							{
								int index = int.Parse(text3.Split(new char[]
								{
									'_'
								})[1].Trim());
								uint value = 0u;
								if (HighLogic.LoadedSceneIsEditor)
								{
									value = EditorLogic.SortedShipList[index].craftID;
								}
								else if (HighLogic.LoadedSceneIsFlight)
								{
									value = this.vessel.parts[index].craftID;
								}
								configNode.AddValue("tgt", value);
							}
							break;
						case "dir":
							configNode.AddValue("dir", text3);
							break;
						case "pos":
							configNode.AddValue("pos", text3);
							break;
						case "rot":
							configNode.AddValue("rot", text3);
							break;
						}
					}
				}
			}
			return configNode;
		}
*/
	}
}

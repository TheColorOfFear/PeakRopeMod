using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Photon.Pun;
using UnityEngine;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]
[assembly: AssemblyCompany("PeakRopes")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
[assembly: AssemblyProduct("My first plugin")]
[assembly: AssemblyTitle("PeakRopes")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]

namespace PeakRopes
{
	[BepInPlugin("PeakRopes", "My first plugin", "1.0.0")]
	public class Plugin : BaseUnityPlugin
	{
		internal static ManualLogSource Log;

		private void Awake()
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			Log = Logger;
			new Harmony("PeakRopes").PatchAll(typeof(ChainedPatch));
		}
	}
	[HarmonyPatch(typeof(Character), "Awake")]
	public static class ChainedPatch
	{
		[HarmonyPostfix]
		public static void AwakePatch(Character __instance)
		{
			if ((UnityEngine.Object)(object)((Component)__instance).gameObject.GetComponent<ChainedController>() == (UnityEngine.Object)null)
			{
				((Component)__instance).gameObject.AddComponent<ChainedController>();
			}
		}
	}
	public class ChainedController : MonoBehaviourPunCallbacks
	{
		public static bool DebugOverlayEnabled = false;

		private Character character;

		private Transform myHip;

		private Rigidbody myRigidbody;

		private static bool isChainedActive;

		private int localPlayerIndex = -1;

		private Character leftDebugTarget;

		private Character rightDebugTarget;

		private float leftDebugDistance;

		private float rightDebugDistance;

		private const float ChainLength = 4f;

		private const float ChainSlack = 0.5f;

		private const float MaxRopeLength = 4f;

		private const float PullStrength = 200f; // was 20f

		private const float SuspensionStrength = 28f;

		private const float SuspensionDamping = 10f;

		private const float MaxPullForce = 1100f; // was 110f

		private Dictionary<string, LineRenderer> chainLines = new Dictionary<string, LineRenderer>();

		private static List<Character> sortedPlayers = new List<Character>();

		private static Material chainMaterial;

		private float nextCacheUpdate = 0f;

		private static GUIStyle debugStyle;

		private void Start()
		{
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Expected O, but got Unknown
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			character = ((Component)this).GetComponent<Character>();
			myHip = GetHip(((Component)this).transform);
			myRigidbody = (((UnityEngine.Object)(object)myHip != (UnityEngine.Object)null) ? ((Component)myHip).GetComponent<Rigidbody>() : null);
			if ((UnityEngine.Object)(object)chainMaterial == (UnityEngine.Object)null)
			{
				chainMaterial = new Material(Shader.Find("Sprites/Default"));
				chainMaterial.color = new Color(0.6f, 0.6f, 0.6f);
			}
		}

		private void Update()
		{
			if (PhotonNetwork.IsMasterClient && (UnityEngine.Object)(object)character != (UnityEngine.Object)null && character.IsLocal && Input.GetKeyDown((KeyCode)289))
			{
				((MonoBehaviourPun)this).photonView.RPC("RpcToggleChain", (RpcTarget)0, new object[1] { !isChainedActive });
			}
			if ((UnityEngine.Object)(object)character == (UnityEngine.Object)null || !character.IsLocal)
			{
				return;
			}
			if (!isChainedActive)
			{
				if (chainLines.Count > 0)
				{
					DestroyAllLines();
				}
			}
			else if (PhotonNetwork.InRoom && PhotonNetwork.PlayerList.Length < 2)
			{
				leftDebugTarget = null;
				rightDebugTarget = null;
				leftDebugDistance = 0f;
				rightDebugDistance = 0f;
				if (chainLines.Count > 0)
				{
					DestroyAllLines();
				}
			}
			else
			{
				if (Time.time > nextCacheUpdate)
				{
					UpdateSortedPlayerList();
					nextCacheUpdate = Time.time + 1.5f;
				}
				DrawAllConnections();
			}
		}

		private void FixedUpdate()
		{
			if (isChainedActive && !((UnityEngine.Object)(object)character == (UnityEngine.Object)null) && character.IsLocal && IsEligibleCharacter(character))
			{
				ApplyChainPhysics();
			}
		}

		private void OnGUI()
		{
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Expected O, but got Unknown
			if (DebugOverlayEnabled && !((UnityEngine.Object)(object)character == (UnityEngine.Object)null) && character.IsLocal)
			{
				if (debugStyle == null)
				{
					GUIStyle val = new GUIStyle(GUI.skin.label)
					{
						fontSize = 16
					};
					val.normal.textColor = Color.white;
					debugStyle = val;
				}
				GUILayout.BeginArea(new Rect(12f, 12f, 720f, 220f), GUI.skin.box);
				GUILayout.Label("PeakRopes: " + (isChainedActive ? "ON" : "OFF"), debugStyle, Array.Empty<GUILayoutOption>());
				GUILayout.Label("Joueur local: " + DescribeCharacter(character), debugStyle, Array.Empty<GUILayoutOption>());
				GUILayout.Label($"Index dans la chaîne: {localPlayerIndex}/{Mathf.Max(0, sortedPlayers.Count - 1)}", debugStyle, Array.Empty<GUILayoutOption>());
				GUILayout.Label($"Cibles détectées: {sortedPlayers.Count}", debugStyle, Array.Empty<GUILayoutOption>());
				GUILayout.Label("Cible active: " + DescribeActiveTarget(), debugStyle, Array.Empty<GUILayoutOption>());
				GUILayout.Label("Gauche: " + DescribeTarget(leftDebugTarget, leftDebugDistance), debugStyle, Array.Empty<GUILayoutOption>());
				GUILayout.Label("Droite: " + DescribeTarget(rightDebugTarget, rightDebugDistance), debugStyle, Array.Empty<GUILayoutOption>());
				GUILayout.EndArea();
			}
		}

		[PunRPC]
		public void RpcToggleChain(bool state)
		{
			Plugin.Log.LogInfo((object)("Chain toggled: " + (state ? "ON" : "OFF")));
			isChainedActive = state;
			if (!isChainedActive)
			{
				leftDebugTarget = null;
				rightDebugTarget = null;
				DestroyAllLines();
			}
		}

		private void UpdateSortedPlayerList()
		{
			if (PhotonNetwork.InRoom && PhotonNetwork.PlayerList.Length < 2)
			{
				sortedPlayers.Clear();
				return;
			}
			sortedPlayers = (from c in UnityEngine.Object.FindObjectsOfType<Character>().Where(IsEligibleCharacter)
				orderby ((MonoBehaviourPun)c).photonView.OwnerActorNr, ((MonoBehaviourPun)c).photonView.ViewID
				select c).ToList();
		}

		private void ApplyChainPhysics()
		{
			localPlayerIndex = -1;
			leftDebugTarget = null;
			rightDebugTarget = null;
			leftDebugDistance = 0f;
			rightDebugDistance = 0f;
			if ((PhotonNetwork.InRoom && PhotonNetwork.PlayerList.Length < 2) || (UnityEngine.Object)(object)myHip == (UnityEngine.Object)null || sortedPlayers.Count < 2)
			{
				return;
			}
			int num = sortedPlayers.IndexOf(character);
			if (num != -1)
			{
				localPlayerIndex = num;
				if (num > 0)
				{
					Character target = (leftDebugTarget = sortedPlayers[num - 1]);
					leftDebugDistance = GetTargetDistance(target);
					ApplyPull(target);
				}
				if (num < sortedPlayers.Count - 1)
				{
					Character target2 = (rightDebugTarget = sortedPlayers[num + 1]);
					rightDebugDistance = GetTargetDistance(target2);
					ApplyPull(target2);
				}
			}
		}

		private void ApplyPull(Character target)
		{
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_012e: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0156: Unknown result type (might be due to invalid IL or missing references)
			if ((UnityEngine.Object)(object)target == (UnityEngine.Object)null || (UnityEngine.Object)(object)target == (UnityEngine.Object)(object)character)
			{
				return;
			}
			Transform hip = GetHip(((Component)target).transform);
			if ((UnityEngine.Object)(object)hip == (UnityEngine.Object)null || (UnityEngine.Object)(object)myRigidbody == (UnityEngine.Object)null)
			{
				return;
			}
			float distance = Vector3.Distance(myHip.position, hip.position);
			Vector3 val = hip.position - myHip.position;
			Vector3 normalized = ((Vector3)(val)).normalized;
			float stretchedChainLength = ChainLength + 0.5f;
			if (distance > stretchedChainLength)
			{
				float newDiff = distance - stretchedChainLength;
				float num4 = Mathf.Clamp01(newDiff / ChainLength);
				float pullForce = newDiff * PullStrength * (0.35f + num4);
				myRigidbody.AddForce(normalized * Mathf.Min(pullForce, MaxPullForce), (ForceMode)5);
			}
			if (distance > MaxRopeLength)
			{
				float ropeDiff = distance - MaxRopeLength;
				float num7 = ropeDiff * SuspensionStrength;
				myRigidbody.AddForce(normalized * Mathf.Min(num7, 165f), (ForceMode)5);
				float dotProduct = Vector3.Dot(myRigidbody.linearVelocity, normalized);
				if (dotProduct < 0f) // dotProduct is negative if we're going in roughly the opposite direction of `normalized`
				{
					myRigidbody.AddForce(normalized * ((0f - dotProduct) * SuspensionDamping), (ForceMode)5);
				}
			}
		}

		private void DrawAllConnections()
		{
			//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
			HashSet<string> activeLineKeys = new HashSet<string>();
			for (int i = 0; i < sortedPlayers.Count - 1; i++)
			{
				Character val = sortedPlayers[i];
				Character val2 = sortedPlayers[i + 1];
				if (!((UnityEngine.Object)(object)val == (UnityEngine.Object)null) && !((UnityEngine.Object)(object)val2 == (UnityEngine.Object)null))
				{
					Transform hip = GetHip(((Component)val).transform);
					Transform hip2 = GetHip(((Component)val2).transform);
					if ((UnityEngine.Object)(object)hip != (UnityEngine.Object)null && (UnityEngine.Object)(object)hip2 != (UnityEngine.Object)null && !((UnityEngine.Object)(object)((MonoBehaviourPun)val).photonView == (UnityEngine.Object)null) && !((UnityEngine.Object)(object)((MonoBehaviourPun)val2).photonView == (UnityEngine.Object)null))
					{
						string text = $"line_{((MonoBehaviourPun)val).photonView.ViewID}_{((MonoBehaviourPun)val2).photonView.ViewID}";
						UpdateLine(text, hip.position, hip2.position);
						activeLineKeys.Add(text);
					}
				}
			}
			List<string> list = chainLines.Keys.Where((string k) => !activeLineKeys.Contains(k)).ToList();
			foreach (string item in list)
			{
				if ((UnityEngine.Object)(object)chainLines[item] != (UnityEngine.Object)null)
				{
					UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)chainLines[item]).gameObject);
				}
				chainLines.Remove(item);
			}
		}

		private float GetTargetDistance(Character target)
		{
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			if ((UnityEngine.Object)(object)target == (UnityEngine.Object)null || (UnityEngine.Object)(object)myHip == (UnityEngine.Object)null)
			{
				return 0f;
			}
			Transform hip = GetHip(((Component)target).transform);
			if ((UnityEngine.Object)(object)hip == (UnityEngine.Object)null)
			{
				return 0f;
			}
			return Vector3.Distance(myHip.position, hip.position);
		}

		private void UpdateLine(string key, Vector3 start, Vector3 end)
		{
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			if (!chainLines.TryGetValue(key, out var value))
			{
				GameObject val = new GameObject(key);
				value = val.AddComponent<LineRenderer>();
				value.positionCount = 2;
				((Renderer)value).material = chainMaterial;
				value.startWidth = 0.08f;
				value.endWidth = 0.08f;
				chainLines[key] = value;
			}
			value.SetPosition(0, start);
			value.SetPosition(1, end);
		}

		private static Transform GetHip(Transform root)
		{
			if ((UnityEngine.Object)(object)root == (UnityEngine.Object)null)
			{
				return null;
			}
			return ((IEnumerable<Transform>)((Component)root).GetComponentsInChildren<Transform>()).FirstOrDefault((Func<Transform, bool>)((Transform t) => ((UnityEngine.Object)t).name == "Hip"));
		}

		private static bool IsEligibleCharacter(Character c)
		{
			return (UnityEngine.Object)(object)c != (UnityEngine.Object)null && ((Behaviour)c).isActiveAndEnabled && !(UnityEngine.Object)(object)c.Ghost && c.IsRegisteredToPlayer && (UnityEngine.Object)(object)c.player != (UnityEngine.Object)null && (UnityEngine.Object)(object)((MonoBehaviourPun)c).photonView != (UnityEngine.Object)null && ((MonoBehaviourPun)c).photonView.OwnerActorNr > 0 && ((MonoBehaviourPun)c).photonView.Owner != null && (UnityEngine.Object)(object)GetHip(((Component)c).transform) != (UnityEngine.Object)null;
		}

		private static string DescribeCharacter(Character c)
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			if ((UnityEngine.Object)(object)c == (UnityEngine.Object)null)
			{
				return "<aucune>";
			}
			Transform hip = GetHip(((Component)c).transform);
			string text = (((UnityEngine.Object)(object)hip != (UnityEngine.Object)null) ? $"{hip.position:F2}" : "<sans Hip>");
			string[] obj = new string[7] { c.characterName, " | Actor ", null, null, null, null, null };
			PhotonView photonView = ((MonoBehaviourPun)c).photonView;
			obj[2] = ((photonView != null) ? photonView.OwnerActorNr.ToString() : null) ?? "?";
			obj[3] = " | ViewID ";
			PhotonView photonView2 = ((MonoBehaviourPun)c).photonView;
			obj[4] = ((photonView2 != null) ? photonView2.ViewID.ToString() : null) ?? "?";
			obj[5] = " | Pos ";
			obj[6] = text;
			return string.Concat(obj);
		}

		private static string DescribeTarget(Character c, float distance)
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			if ((UnityEngine.Object)(object)c == (UnityEngine.Object)null)
			{
				return "<aucune cible valide>";
			}
			Transform hip = GetHip(((Component)c).transform);
			object obj;
			if (!((UnityEngine.Object)(object)hip != (UnityEngine.Object)null))
			{
				obj = "<sans Hip>";
			}
			else
			{
				Vector3 position = hip.position;
				obj = ((Vector3)(position)).ToString("F2");
			}
			string text = (string)obj;
			object[] obj2 = new object[5] { c.characterName, null, null, null, null };
			PhotonView photonView = ((MonoBehaviourPun)c).photonView;
			obj2[1] = ((photonView != null) ? photonView.OwnerActorNr.ToString() : null) ?? "?";
			PhotonView photonView2 = ((MonoBehaviourPun)c).photonView;
			obj2[2] = ((photonView2 != null) ? photonView2.ViewID.ToString() : null) ?? "?";
			obj2[3] = distance;
			obj2[4] = text;
			return string.Format("{0} | Actor {1} | ViewID {2} | Dist {3:F2} | Pos {4}", obj2);
		}

		private string DescribeActiveTarget()
		{
			if ((UnityEngine.Object)(object)leftDebugTarget == (UnityEngine.Object)null && (UnityEngine.Object)(object)rightDebugTarget == (UnityEngine.Object)null)
			{
				return "<aucune>";
			}
			if ((UnityEngine.Object)(object)leftDebugTarget != (UnityEngine.Object)null && (UnityEngine.Object)(object)rightDebugTarget != (UnityEngine.Object)null)
			{
				return "G=" + leftDebugTarget.characterName + " / D=" + rightDebugTarget.characterName;
			}
			return ((UnityEngine.Object)(object)leftDebugTarget != (UnityEngine.Object)null) ? ("G=" + leftDebugTarget.characterName) : ("D=" + rightDebugTarget.characterName);
		}

		private void DestroyAllLines()
		{
			foreach (LineRenderer value in chainLines.Values)
			{
				if ((UnityEngine.Object)(object)value != (UnityEngine.Object)null)
				{
					UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)value).gameObject);
				}
			}
			chainLines.Clear();
		}

		public override void OnDisable()
		{
			((MonoBehaviourPunCallbacks)this).OnDisable();
			DestroyAllLines();
		}

		private void OnDestroy()
		{
			DestroyAllLines();
		}
	}
}

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR.Features;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace RRX.Runtime.OpenXRFeatures
{
    /// <summary>
    /// Suppresses the Meta Quest guardian / boundary system while the app is focused, so the user can walk
    /// freely inside their configured Room / Space without the OS fading the virtual scene to passthrough
    /// as they approach the play-area edge. Implemented via the OpenXR extension
    /// <c>XR_META_boundary_visibility</c> (Meta Quest runtime v60+). If the extension is not exposed by the
    /// current OpenXR runtime, the feature no-ops and logs a notice — the default guardian behavior then
    /// applies.
    /// </summary>
    /// <remarks>
    /// The extension is requested in every session at the <c>XR_SESSION_STATE_FOCUSED</c> transition so the
    /// suppression survives transient focus loss (e.g. system overlay). Use responsibly — the user is
    /// still responsible for the real-world safety of the space they walk in.
    /// </remarks>
#if UNITY_EDITOR
    [OpenXRFeature(
        UiName = "RRX: Suppress Meta Quest Guardian",
        BuildTargetGroups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone },
        Company = "RRX",
        Desc = "Suppresses the Quest guardian/boundary fade via XR_META_boundary_visibility so the player can roam in MR inside their configured room without the view dimming.",
        OpenxrExtensionStrings = "XR_META_boundary_visibility",
        Category = FeatureCategory.Feature,
        FeatureId = FeatureId,
        Version = "1.0.0")]
#endif
    public sealed class RRXBoundaryVisibilityFeature : OpenXRFeature
    {
        /// <summary>Unique feature identifier used by the OpenXR package settings.</summary>
        public const string FeatureId = "com.rrx.openxr.feature.boundary-visibility-suppress";

        // ReSharper disable InconsistentNaming
        const int XR_SUCCESS = 0;
        const int XR_SESSION_STATE_FOCUSED = 6;
        const int XR_BOUNDARY_VISIBILITY_NOT_SUPPRESSED_META = 1;
        const int XR_BOUNDARY_VISIBILITY_SUPPRESSED_META = 2;
        // ReSharper restore InconsistentNaming

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int XrGetInstanceProcAddrDelegate(
            ulong instance,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            out IntPtr function);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int XrRequestBoundaryVisibilityMETADelegate(ulong session, int boundaryVisibility);

        XrRequestBoundaryVisibilityMETADelegate _requestBoundaryVisibility;
        ulong _session;
        bool _extensionAvailable;
        bool _appliedForCurrentSession;

        protected override bool OnInstanceCreate(ulong xrInstance)
        {
            _extensionAvailable = false;
            _requestBoundaryVisibility = null;

            var procPtr = xrGetInstanceProcAddr;
            if (procPtr == IntPtr.Zero)
                return true;

            XrGetInstanceProcAddrDelegate getProc;
            try
            {
                getProc = Marshal.GetDelegateForFunctionPointer<XrGetInstanceProcAddrDelegate>(procPtr);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RRX] Failed to bind xrGetInstanceProcAddr: {e.Message}");
                return true;
            }

            int result = getProc(xrInstance, "xrRequestBoundaryVisibilityMETA", out IntPtr fnPtr);
            if (result != XR_SUCCESS || fnPtr == IntPtr.Zero)
            {
                Debug.Log(
                    "[RRX] XR_META_boundary_visibility not available on this runtime; Quest guardian fade will use system defaults.");
                return true;
            }

            _requestBoundaryVisibility =
                Marshal.GetDelegateForFunctionPointer<XrRequestBoundaryVisibilityMETADelegate>(fnPtr);
            _extensionAvailable = true;
            return true;
        }

        protected override void OnSessionCreate(ulong xrSession)
        {
            _session = xrSession;
            _appliedForCurrentSession = false;
            TrySuppressGuardian();
        }

        protected override void OnSessionStateChange(int oldState, int newState)
        {
            if (newState == XR_SESSION_STATE_FOCUSED)
                TrySuppressGuardian();
        }

        protected override void OnSessionDestroy(ulong xrSession)
        {
            _session = 0;
            _appliedForCurrentSession = false;
        }

        void TrySuppressGuardian()
        {
            if (!_extensionAvailable || _requestBoundaryVisibility == null || _session == 0)
                return;

            int result = _requestBoundaryVisibility(_session, XR_BOUNDARY_VISIBILITY_SUPPRESSED_META);
            if (result == XR_SUCCESS)
            {
                if (!_appliedForCurrentSession)
                {
                    Debug.Log("[RRX] Meta Quest guardian suppressed (XR_META_boundary_visibility).");
                    _appliedForCurrentSession = true;
                }
            }
            else
            {
                Debug.LogWarning(
                    $"[RRX] xrRequestBoundaryVisibilityMETA returned {result}; guardian fade may still trigger.");
            }
        }
    }
}

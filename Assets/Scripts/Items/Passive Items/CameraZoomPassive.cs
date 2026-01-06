using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;  // Add this for Cinemachine support

public class CameraZoomPassive : Passive
{
    [Header("Camera Zoom Settings")]
    [SerializeField] private float zoomIncrease = 2f;  // How much to increase the camera's orthographic size
    [SerializeField] private CinemachineVirtualCamera virtualCamera;  // Reference to the Cinemachine virtual camera

    private float originalSize;  // Store the original orthographic size to revert on unequip

    public override void OnEquip()
    {
        base.OnEquip();

        // Get the virtual camera if not assigned
        if (virtualCamera == null)
        {
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();  // Find the first one in the scene
        }

        if (virtualCamera != null)
        {
            // Store the original size
            originalSize = virtualCamera.m_Lens.OrthographicSize;

            // Increase the camera's view via the virtual camera's lens
            virtualCamera.m_Lens.OrthographicSize += zoomIncrease;

            Debug.Log($"Camera zoom increased to {virtualCamera.m_Lens.OrthographicSize} for passive: {data.name}");
        }
        else
        {
            Debug.LogWarning("No CinemachineVirtualCamera found for CameraZoomPassive!");
        }
    }

    public override void OnUnequip()
    {
        base.OnUnequip();

        if (virtualCamera != null)
        {
            // Revert to the original size
            virtualCamera.m_Lens.OrthographicSize = originalSize;

            Debug.Log($"Camera zoom reverted to {originalSize} for passive: {data.name}");
        }
    }
}
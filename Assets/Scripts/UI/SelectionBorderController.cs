using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SelectionBorderController : MonoBehaviour
{
    [Header("References")]
    public RectTransform border;              // The border highlight (outside the layout group)
    public RectTransform optionsHolder;       // The Upgrade Options Holder (with VerticalLayoutGroup)
    public PlayerInput playerInput;           // PlayerInput component with UI actions

    [Header("Settings")]
    public string optionTag = "UpgradeOption"; // Tag all Upgrade Option objects with this tag
    public int currentIndex = 0;

    private List<RectTransform> upgradeOptions = new List<RectTransform>();
    private InputAction navigateAction;
    private InputAction submitAction;         // NEW: For confirming/clicking the selected option
    private GameObject lastSelectedObject;  // Track the last selected object to detect changes

    private float lastNavigateTime = 0f;
    private float navigateCooldown = 0.2f; // 200ms between moves

    void Awake()
    {
        if (!playerInput)
        {
            playerInput = FindObjectOfType<PlayerInput>();
        }

        if (playerInput != null)
        {
            // Make sure we use the UI action map
            playerInput.SwitchCurrentActionMap("UI");
            navigateAction = playerInput.actions["Navigate"];
            navigateAction.performed += OnNavigate;

            // NEW: Set up Submit action for confirmation (includes spacebar if bound)
            submitAction = playerInput.actions["Submit"];
            submitAction.performed += OnSubmit;
        }

        // Disable raycasting on the border to prevent blocking mouse clicks
        Image borderImage = border.GetComponent<Image>();
        if (borderImage != null)
        {
            borderImage.raycastTarget = false;
        }
    }

    void Start()
    {
        RefreshOptions();  // Initial setup
    }

    void Update()
    {
        // Check if the EventSystem's selected object has changed and update the border
        GameObject currentSelected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        if (currentSelected != lastSelectedObject && currentSelected != null)
        {
            for (int i = 0; i < upgradeOptions.Count; i++)
            {
                Button btn = upgradeOptions[i].Find("Button")?.GetComponent<Button>();
                if (btn != null && btn.gameObject == currentSelected)
                {
                    currentIndex = i;
                    MoveBorderTo(upgradeOptions[i]);
                    lastSelectedObject = currentSelected;
                    break;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (navigateAction != null)
            navigateAction.performed -= OnNavigate;
        if (submitAction != null)  // NEW: Unsubscribe from Submit
            submitAction.performed -= OnSubmit;
    }

    private void OnNavigate(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();

        // Ignore small accidental stick movements
        if (Mathf.Abs(input.y) < 0.5f)
            return;

        // Prevent multiple triggers from one press
        if (Time.time - lastNavigateTime < navigateCooldown)
            return;

        lastNavigateTime = Time.time;

        if (input.y > 0.5f)
        {
            ChangeSelection(-1); // Up
        }
        else if (input.y < -0.5f)
        {
            ChangeSelection(1); // Down
        }
    }

    // NEW: Handle confirmation/submit (e.g., spacebar or Enter)
    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (upgradeOptions.Count == 0) return;

        // Get the button of the currently selected option
        Button button = upgradeOptions[currentIndex].Find("Button")?.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.Invoke();  // Simulate a click
            Debug.Log($"SelectionBorderController: Confirmed selection at index {currentIndex} ({upgradeOptions[currentIndex].name})");
        }
        else
        {
            Debug.LogWarning("SelectionBorderController: No button found to confirm!");
        }
    }

    void ChangeSelection(int direction)
    {
        if (upgradeOptions.Count == 0) return;

        int newIndex = currentIndex + direction;

        // Clamp the index between 0 and last index
        newIndex = Mathf.Clamp(newIndex, 0, upgradeOptions.Count - 1);

        // Only update if the index actually changed
        if (newIndex != currentIndex)
        {
            currentIndex = newIndex;
            MoveBorderTo(upgradeOptions[currentIndex]);
        }
    }

    void MoveBorderTo(RectTransform target)
    {
        // Position the border to match the target's position and size
        border.position = target.position;
        border.sizeDelta = target.sizeDelta;

        // Set the EventSystem's selected object to the button inside the target
        Button button = target.Find("Button")?.GetComponent<Button>();  // Adjust path based on your UI structure
        if (button != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
            lastSelectedObject = button.gameObject;
            Debug.Log($"SelectionBorderController: Selected option at index {currentIndex} ({target.name})");
        }
        else
        {
            Debug.LogWarning("SelectionBorderController: No button found in target RectTransform!");
        }
    }

    // Public method to refresh the options list (call this after UIUpgradeWindow.SetUpgrades)
    public void RefreshOptions()
    {
        upgradeOptions.Clear();
        foreach (Transform child in optionsHolder)
        {
            if (child.CompareTag(optionTag) && child.gameObject.activeSelf)
                upgradeOptions.Add(child as RectTransform);
        }

        if (upgradeOptions.Count > 0)
        {
            currentIndex = Mathf.Clamp(currentIndex, 0, upgradeOptions.Count - 1);
            MoveBorderTo(upgradeOptions[currentIndex]);
            Debug.Log($"SelectionBorderController: Refreshed options list with {upgradeOptions.Count} items.");
        }
        else
        {
            Debug.LogWarning("SelectionBorderController: No active upgrade options found after refresh!");
        }
    }
}

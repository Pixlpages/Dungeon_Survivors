using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UIKeyboardNavigator : MonoBehaviour
{
    [SerializeField] private List<Button> buttons;  // Assign your buttons in the Inspector
    private int currentIndex = 0;

    void Start()
    {
        if (buttons == null || buttons.Count == 0)
        {
            Debug.LogWarning("UIKeyboardNavigator: No buttons assigned!");
            return;
        }

        // Select the first button initially
        SelectButton(currentIndex);
    }

    void Update()
    {
        if (buttons == null || buttons.Count == 0) return;

        // Navigate Up
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            currentIndex--;
            if (currentIndex < 0) currentIndex = buttons.Count - 1;
            SelectButton(currentIndex);
        }

        // Navigate Down
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            currentIndex++;
            if (currentIndex >= buttons.Count) currentIndex = 0;
            SelectButton(currentIndex);
        }

        // Press Enter to "click" the selected button
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.Space))
        {
            buttons[currentIndex].onClick.Invoke();
        }
    }

    private void SelectButton(int index)
    {
        // Visually highlight & select via EventSystem
        EventSystem.current.SetSelectedGameObject(buttons[index].gameObject);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

//we require verticalLayoutGroup on the the gameobject this is attached to, because it uses the component
//to make sure buttons are evenly spaced out
[RequireComponent(typeof(VerticalLayoutGroup))]
public class UIUpgradeWindow : MonoBehaviour
{
    //we will need to access the padding / spacing attributes on the layout
    VerticalLayoutGroup verticalLayout;

    //the button and tooltip template GameObjects we have to assign
    public RectTransform upgradeOptionTemplate;
    public TextMeshProUGUI tooltipTemplate;

    [Header("Settings")]
    public int maxOptions = 4; //we cannot show more options than this
    public string newText = "New!"; //the text that shows when a new upgrade is shown

    //color of the "New!" text and the regular text
    public Color newTextColor = Color.yellow, levelTextColor = Color.white;

    //these are the paths to the different UI elements in the <upgradeOptionTemplate>
    [Header("Paths")]
    public string iconPath = "Icon/Item Icon";
    public string namePath = "Name", descriptionPath = "Description", buttonPath = "Button", levelPath = "Level";

    //private variables
    RectTransform rectTransform;
    float optionHeight;
    int activeOptions;

    //this is a list of all the upgrade buttons on the window
    List<RectTransform> upgradeOptions = new List<RectTransform>();

    //track last screen size
    Vector2 lastScreen;


    public void SelectFirstOption()
    {
        // Find the first active button
        foreach (RectTransform r in upgradeOptions)
        {
            if (!r.gameObject.activeSelf) continue;
            Button b = r.Find(buttonPath)?.GetComponent<Button>();
            if (b != null)
            {
                EventSystem.current.SetSelectedGameObject(b.gameObject);
                break;
            }
        }
    }

    //this is the main function we will be calling
    public void SetUpgrades(PlayerInventory inventory, List<ItemData> possibleUpgrades, int pick = 3, string tooltip = "")
    {
        pick = Mathf.Min(maxOptions, pick);

        //if we don't have enough upgrade option boxes, create them
        if (maxOptions > upgradeOptions.Count)
        {
            for (int i = upgradeOptions.Count; i < pick; i++)
            {
                GameObject go = Instantiate(upgradeOptionTemplate.gameObject, transform);
                upgradeOptions.Add((RectTransform)go.transform);
            }
        }

        //if a string is provided, turn on the tooltip
        tooltipTemplate.text = tooltip;
        tooltipTemplate.gameObject.SetActive(tooltip.Trim() != "");

        //activate only the number of upgrade options we need
        activeOptions = 0;
        int totalPossibleUpgrades = possibleUpgrades.Count;
        foreach (RectTransform r in upgradeOptions)
        {
            if (activeOptions < pick && activeOptions < totalPossibleUpgrades)
            {
                r.gameObject.SetActive(true);

                //tag this object as an upgrade option so SelectionBorderController can find it
                r.gameObject.tag = "UpgradeOption";

                //select one of the possible upgrades, then remove it from the list
                ItemData selected = possibleUpgrades[Random.Range(0, possibleUpgrades.Count)];
                possibleUpgrades.Remove(selected);
                Item item = inventory.Get(selected);

                //Insert the name of the item
                TextMeshProUGUI name = r.Find(namePath).GetComponent<TextMeshProUGUI>();
                if (name) name.text = selected.name;

                //insert the current level of the item, or "New!" if new
                TextMeshProUGUI level = r.Find(levelPath).GetComponent<TextMeshProUGUI>();
                if (level)
                {
                    if (item)
                    {
                        if (item.currentLevel >= item.maxLevel)
                        {
                            level.text = "Max!";
                            level.color = newTextColor;
                        }
                        else
                        {
                            level.text = selected.GetLevelData(item.currentLevel + 1).name;
                            level.color = levelTextColor;
                        }
                    }
                    else
                    {
                        level.text = newText;
                        level.color = newTextColor;
                    }
                }

                //insert the description
                TextMeshProUGUI desc = r.Find(descriptionPath).GetComponent<TextMeshProUGUI>();
                if (desc)
                {
                    if (item)
                    {
                        var nextLevel = item.currentLevel + 1;
                        var levelData = selected.GetLevelData(nextLevel);
                        if (levelData != null)
                            desc.text = levelData.description;
                        else
                            desc.text = selected.GetLevelData(1)?.description ?? "";
                    }
                    else
                    {
                        desc.text = selected.GetLevelData(1)?.description ?? "";
                    }
                }

                //Insert the icon
                Image icon = r.Find(iconPath).GetComponent<Image>();
                if (icon) icon.sprite = selected.icon;

                //insert button action binding
                Button b = r.Find(buttonPath).GetComponent<Button>();
                if (b)
                {
                    b.onClick.RemoveAllListeners();
                    if (item)
                        b.onClick.AddListener(() => inventory.LevelUp(item));
                    else
                        b.onClick.AddListener(() => inventory.Add(selected));
                }

                activeOptions++;
            }
            else
            {
                r.gameObject.SetActive(false);
                r.gameObject.tag = "Untagged"; // make sure inactive ones are ignored
            }
        }

        //sizes all the elements
        RecalculateLayout();

        // Refresh the SelectionBorderController's options list after setup
        SelectionBorderController borderController = FindObjectOfType<SelectionBorderController>();
        if (borderController != null)
        {
            borderController.RefreshOptions();
        }
    }

    //Recalculate heights
    void RecalculateLayout()
    {
        optionHeight = rectTransform.rect.height - verticalLayout.padding.top - verticalLayout.padding.bottom
        - (maxOptions - 1) * verticalLayout.spacing;

        if (activeOptions == maxOptions && tooltipTemplate.gameObject.activeSelf)
            optionHeight /= maxOptions + 1;
        else
            optionHeight /= maxOptions;

        //tooltip
        if (tooltipTemplate.gameObject.activeSelf)
        {
            RectTransform tooltipRect = (RectTransform)tooltipTemplate.transform;
            tooltipTemplate.gameObject.SetActive(true);
            tooltipRect.sizeDelta = new Vector2(tooltipRect.sizeDelta.x, optionHeight);
            tooltipTemplate.transform.SetAsLastSibling();
        }

        //options
        foreach (RectTransform r in upgradeOptions)
        {
            if (!r.gameObject.activeSelf) continue;
            r.sizeDelta = new Vector2(r.sizeDelta.x, optionHeight);
        }
    }

    void Update()
    {
        if (EventSystem.current.currentSelectedGameObject == null)
            SelectFirstOption();

        if (lastScreen.x != Screen.width || lastScreen.y != Screen.height)
        {
            RecalculateLayout();
            lastScreen = new Vector2(Screen.width, Screen.height);
        }
    }

    void Awake()
    {
        verticalLayout = GetComponentInChildren<VerticalLayoutGroup>();
        if (tooltipTemplate) tooltipTemplate.gameObject.SetActive(false);
        if (upgradeOptionTemplate) upgradeOptions.Add(upgradeOptionTemplate);

        rectTransform = (RectTransform)transform;
    }

    void Reset()
    {
        upgradeOptionTemplate = (RectTransform)transform.Find("Upgrade Option");
        tooltipTemplate = transform.Find("Tooltip").GetComponentInChildren<TextMeshProUGUI>();
    }
}

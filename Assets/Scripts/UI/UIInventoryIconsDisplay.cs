using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(LayoutGroup))]
public class UIInventoryIconsDisplay : MonoBehaviour
{
    public GameObject slotTemplate;
    public uint maxSlots = 6;
    public bool showLevels = true;
    public PlayerInventory inventory;

    public GameObject[] slots;

    [Header("Paths")]
    public string iconPath;
    public string levelTextPath;
    [HideInInspector] public string targetedItemList;

    void Reset()
    {
        slotTemplate = transform.GetChild(0).gameObject;
        inventory = FindObjectOfType<PlayerInventory>();
    }

    void OnEnable()
    {
        Refresh();
    }

    // this will read the inventory and see if teher are any new updates
    //to the items on the PlayerCharacter
    public void Refresh()
    {
        if (!inventory)
            Debug.LogWarning("No Inventory attached to the UI icon display");

        //figure out which inventory I want
        Type t = typeof(PlayerInventory);
        FieldInfo field = t.GetField(targetedItemList, BindingFlags.Public | BindingFlags.Instance);

        //if the given field is not found, then show a warning
        if (field == null)
        {
            Debug.LogWarning("The list in the inventory is not found");
            return;
        }

        //get the list of inventory slots
        List<PlayerInventory.Slot> items = (List<PlayerInventory.Slot>)field.GetValue(inventory);

        //start populating the icons
        for (int i = 0; i < items.Count; i++)
        {
            //check if we have enough slots for the item
            //otherwise print warning
            if (i >= slots.Length)
            {
                Debug.LogWarning(string.Format("You have {0} inventory slots, but only {0} slots on the UI", items.Count, slots.Length));
                break;
            }

            //Get the item data
            Item item = items[i].item;

            Transform iconObj = slots[i].transform.Find(iconPath);
            if (iconObj)
            {
                Image icon = iconObj.GetComponentInChildren<Image>();

                //if the item doesn't exist, make the icon transparent
                if (!item)
                    icon.color = new Color(1, 1, 1, 0);
                else
                {
                    icon.color = new Color(1, 1, 1, 1);
                    if (icon)
                        icon.sprite = item.data.icon;
                }
            }

            //set the level as well
            Transform levelObj = slots[i].transform.Find(levelTextPath);
            if (levelObj)
            {
                //find the text component and put the level inside
                TextMeshProUGUI levelTxt = levelObj.GetComponentInChildren<TextMeshProUGUI>();
                if (levelTxt)
                {
                    if (!item || !showLevels)
                        levelTxt.text = "";
                    else
                        levelTxt.text = item.currentLevel.ToString();
                }
            }
        }
    }
}

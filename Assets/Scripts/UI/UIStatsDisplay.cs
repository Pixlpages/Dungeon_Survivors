using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Text;
using System.Reflection;
using System;

public class UIStatsDisplay : MonoBehaviour
{
    public PlayerStats player;
    public bool displayCurrentHealth = false;
    public bool updateInEditor = false;
    TextMeshProUGUI statNames, statValues;

    void OnEnable() //updates whenever set to be active
    {
        UpdateStatFields();
    }

    void OnDrawGizmosSelected()
    {
        if (updateInEditor)
            UpdateStatFields();
    }

    public void UpdateStatFields()
    {
        if (!player)
            return;

        //Get a reference to both Text objects to render stat names and stat values
        if (!statNames) statNames = transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        if (!statValues) statValues = transform.GetChild(1).GetComponent<TextMeshProUGUI>();

        //Render all stat names and values
        //using StringBuilders so the string manipulation runs faster
        StringBuilder names = new StringBuilder();
        StringBuilder values = new StringBuilder();

        //add the current health into the stat box
        if (displayCurrentHealth)
        {
            names.AppendLine("Health");
            values.AppendLine(player.CurrentHealth.ToString());
        }

        FieldInfo[] fields = typeof(CharacterData.Stats).GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (FieldInfo field in fields)
        {
            //render stat names
            names.AppendLine(field.Name);

            //Get the stat value
            object val = field.GetValue(player.Stats);
            float fval = val is int ? (int)val : (float)val;

            //print is as a percentage if it has an attribute assigned and is a float
            PropertyAttribute attribute = (PropertyAttribute)PropertyAttribute.GetCustomAttribute(field, typeof(PropertyAttribute));
            if (attribute != null && field.FieldType == typeof(float))
            {
                float percentage = Mathf.Round((fval - 1f) * 100f);

                //if the stat value is 0, just put a dash
                if (Mathf.Approximately(percentage, 0))
                {
                    values.Append('-').Append('\n');
                }
                else
                {
                    if (percentage > 0)
                        values.Append('+');
                    else
                        values.Append('-');
                    values.Append(percentage).Append('%').Append('\n');
                }
            }
            else
            {
                values.Append(fval).Append('\n');
            }

            //updates the fields with the strings built
            statNames.text = PrettifyNames(names);
            statValues.text = values.ToString();
        }
    }

    public static string PrettifyNames(StringBuilder input)
    {
        //return an empty string if stringbuilder is empty
        if (input.Length <= 0)
            return string.Empty;

        StringBuilder result = new StringBuilder();
        char last = '\0';
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            //Check when to uppercase or add spaces to a character
            if (last == '\0' || char.IsWhiteSpace(last))
            {
                c = char.ToUpper(c);
            }
            else if (char.IsUpper(c))
            {
                result.Append(' '); // insert space before capital letter
            }
            result.Append(c);

            last = c;
        }

        return result.ToString();
    }

    void Reset()
    {
        player = FindObjectOfType<PlayerStats>();
    }
}

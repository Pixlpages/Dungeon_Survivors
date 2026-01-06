using System.Collections.Generic;
using UnityEngine;

public class PPMTracker : MonoBehaviour
{
    public static PPMTracker Instance;

    private Dictionary<string, int> globalWeaponChoices = new Dictionary<string, int>();
    private Dictionary<string, int> globalPassiveChoices = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    public void RecordWeaponChoice(WeaponData w)
    {
        if (w == null) return;
        if (!globalWeaponChoices.ContainsKey(w.name)) globalWeaponChoices[w.name] = 0;
        globalWeaponChoices[w.name]++;
    }

    public void RecordPassiveChoice(PassiveData p)
    {
        if (p == null) return;
        if (!globalPassiveChoices.ContainsKey(p.name)) globalPassiveChoices[p.name] = 0;
        globalPassiveChoices[p.name]++;
    }

    public Dictionary<string,int> GetWeaponPreferences() => globalWeaponChoices;
    public Dictionary<string,int> GetPassivePreferences() => globalPassiveChoices;
}

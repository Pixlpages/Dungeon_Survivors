using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AuraWeapon : Weapon
{
    protected Aura currentAura;

    protected override void Update()
    {

    }

    public override void OnEquip()
    {
        // if there’s already an aura, remove it properly
        if (currentAura)
        {
            var ps = currentAura.GetComponent<ParticleSystem>();
            if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // clear particles instantly
            Destroy(currentAura.gameObject);
        }

        // spawn new aura prefab
        currentAura = Instantiate(currentStats.auraPrefab, transform);
        currentAura.weapon = this;
        currentAura.owner = owner;
        float area = GetArea();
        currentAura.transform.localScale = new Vector3(area, area, area);
    
    }

    public override void OnUnequip()
    {
        if (currentAura) Destroy(currentAura);
    }

    public override bool DoLevelUp()
    {
        if (!base.DoLevelUp())
            return false;

        OnEquip(); //enure aura is refreshed when a different one is assigned

        //if there is an aura attached to this weapon, we update the aura
        if (currentAura)
        {
            currentAura.transform.localScale = new Vector3(currentStats.area, currentStats.area, currentStats.area);
        }
        return true;
    }
}

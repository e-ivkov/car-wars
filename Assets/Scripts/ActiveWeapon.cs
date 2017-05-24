using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveWeapon : ActiveCarPart
{
    private int ammo;
    public int Ammo
    {
        get
        {
            return ammo;
        }
        set
        {
            if (value < 0)
            {
                ammo = 0;
            }
            if(ammo == 0)
            {
                firing = false;
            }
        }
    }

    private bool firing = false;
    public bool Firing
    {
        get
        {
            return Active && firing;
        }
        set
        {
            firing = value;
        }
    }

    public Vector2 Orientation
    {
        get; private set;
    }

    public ActiveWeapon(ProjectileWeapon part, Vector2 orientation) : base(part)
    {
        Ammo = part.MaxAmmoCapacity;
        Orientation = orientation;
    }
}

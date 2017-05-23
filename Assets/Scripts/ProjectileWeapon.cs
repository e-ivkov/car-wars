using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CarWars/VehicleDesign/ProjectileWeapon")]
public class ProjectileWeapon : DamagableCarPart
{
    public int ToHit;
    public int DamageDiceNumber;
    public int Spaces;
    public int MaxAmmoCapacity;
    public int AmmoCost;
    public int AmmoWeight;
}

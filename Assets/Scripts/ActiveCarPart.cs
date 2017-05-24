using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveCarPart
{

    public DamagableCarPart CarPartType;
    private int dp;
    public int DP
    {
        get
        {
            return dp;
        }

        set
        {
            if (value < 0)
            {
                dp = 0;
            }
            else if (value > CarPartType.MaxDP)
            {
                dp = CarPartType.MaxDP;
            }
        }
    }

    public bool Active
    {
        get
        {
            return dp > 0;
        }
    }

    public ActiveCarPart(DamagableCarPart partType)
    {
        CarPartType = partType;
        dp = partType.MaxDP;
    }
}

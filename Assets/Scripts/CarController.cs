using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;

public class CarController : MonoBehaviour
{

    public int Weight;
    public int Cost;
    public int HandlingClass;
    public int Acceleration;
    public int TopSpeed;
    public float GridSize;

    private int CurrentSpeed = 0;
    private int CurrentHandlingClass;
    private int Phase = 0;
    private int Turn = 0;
    private bool AccelerationPerformed = false;
    private bool ManeuverPerformed = false;

    private float[][] MovementChart = new float[60][];

    private void LoadMoavementChart(string filename, char separator)
    {
        using (var fs = File.OpenRead(filename))
        using (var reader = new StreamReader(fs))
        {
           for(int i = 0; i < 60; i++)
            {
                var line = reader.ReadLine();
                var values = line.Split(separator);
                MovementChart[i] = new float[5];
                for(int j = 0; j < 5; j++)
                {
                    MovementChart[i][j] = float.Parse(values[j]);
                }
            }
        }
    }

    public void Move()
    {
        var v = transform.up * GridSize * MovementChart[CurrentSpeed / 5][Phase];
        Debug.Log(v);
        transform.Translate(v,Space.World);
    }

    public void Bend(float bendAngle)
    {
        if (MovementChart[CurrentSpeed / 5][Phase] >= 1 && !ManeuverPerformed)
        {
            transform.Rotate(0, 0, bendAngle);
            ManeuverPerformed = true;
        }
    }

    public void Accelerate(int acceleration)
    {
        if (!AccelerationPerformed)
        {
            CurrentSpeed += acceleration;
            AccelerationPerformed = true;
        }
    }

    public void PhaseUpdate(bool bendFlag, float bendAngle, bool acceleartionFlag, int acceleration)
    {
        if (bendFlag)
            Bend(bendAngle);
        if (acceleartionFlag)
            Accelerate(acceleration);
        Move();
        if (Phase < 4)
            Phase++;
        else
        {
            Phase = 0;
            AccelerationPerformed = false;
            ManeuverPerformed = false;
            Turn++;
        }
    }

    public void ParsePhaseInput()
    {
        var values = GameObject.Find("InputField").GetComponent<InputField>().text.Split(',');
        PhaseUpdate(bool.Parse(values[0]), float.Parse(values[1]), bool.Parse(values[2]), int.Parse(values[3]));
    }

    // Use this for initialization
    void Start()
    {
        CurrentHandlingClass = HandlingClass;
        LoadMoavementChart("MovementChart.csv", ',');
    }

    // Update is called once per frame
    void Update()
    {

    }
}

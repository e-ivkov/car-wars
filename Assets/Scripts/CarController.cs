using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
namespace CarWars
{
    enum Maneuvers { Bend, Drift, Swerve, BendControlledSkid, SwerveControlledSkid, JTurn, TStop, Fishtail, Pivot, }

    public class CarController : MonoBehaviour
    {

        public int Weight;
        public int Cost;
        public int HandlingClass;
        public int Acceleration;
        public int TopSpeed;
        public float GridSize;

        private int CurrentSpeed = 0;
        private float CurrentDirection = 1;
        private int CurrentHandlingClass;
        private int Phase = 0;
        private int Turn = 0;
        private bool AccelerationPerformed = false;
        private int ManeuverState = 0;              //0 - not performed, 1 - middle phase, 2 - final phase
                                                    //not all maneuvers have the middle phase
        private int CurrentManeuver = -1;
        private Vector3 SkidVector;                 //used only for skid maneuver

        private float[][] MovementChart = new float[60][];
        private int[][] ControlTable = new int[30][];

        private Vector3[] GetCorners()
        {
            BoxCollider2D collider = transform.GetComponent<BoxCollider2D>();

            float top = collider.offset.y + (collider.size.y / 2f);
            float btm = collider.offset.y - (collider.size.y / 2f);
            float left = collider.offset.x - (collider.size.x / 2f);
            float right = collider.offset.x + (collider.size.x / 2f);

            Vector3 topLeft = transform.TransformPoint(new Vector3(left, top, 0f));
            Vector3 topRight = transform.TransformPoint(new Vector3(right, top, 0f));
            Vector3 btmLeft = transform.TransformPoint(new Vector3(left, btm, 0f));
            Vector3 btmRight = transform.TransformPoint(new Vector3(right, btm, 0f));

            return new Vector3[4] { topLeft, topRight, btmLeft, btmRight };
        }

        private void LoadTable<T>(string filename, char separator, T[][] table, int n, int m)
        {
            using (var fs = File.OpenRead(filename))
            using (var reader = new StreamReader(fs))
            {
                for (int i = 0; i < n; i++)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(separator);
                    table[i] = new T[m];
                    for (int j = 0; j < m; j++)
                    {
                        table[i][j] = (T)System.Convert.ChangeType(values[j], typeof(T)); ;
                    }
                }
            }
        }

        private void Move(float dist)
        {
            var v = transform.up * GridSize * dist * CurrentDirection;
            //Debug.Log(v);
            transform.Translate(v, Space.World);
        }

        private void Drift(float driftOffset)
        {
            Move(1);
            transform.Translate(driftOffset, 0, 0);
            CurrentManeuver = (int)Maneuvers.Drift;
            ManeuverState = 2;
        }

        private void Swerve(float bendAngle)
        {
            transform.Translate(Mathf.Sign(bendAngle) * 0.25f * GridSize, 0, 0);
            Bend(bendAngle);
            CurrentManeuver = (int)Maneuvers.Swerve;
            ManeuverState = 2;
        }

        private void Bend(float bendAngle)
        {
            Move(1);
            Vector3[] cornerPoints = GetCorners();
            Vector3 rotationPoint;
            if (bendAngle > 0)
                rotationPoint = cornerPoints[3];
            else
                rotationPoint = cornerPoints[2];
            transform.RotateAround(rotationPoint, transform.forward, bendAngle);
            CurrentManeuver = (int)Maneuvers.Bend;
            ManeuverState = 2;
        }

        private void ControlledSkid(float skidParam, bool bend)
        {
            if (ManeuverState == 0)
            {
                SkidVector = transform.up;
                if (bend)
                {
                    Bend(skidParam);
                    CurrentManeuver = (int)Maneuvers.BendControlledSkid;
                }
                else
                {
                    Swerve(skidParam);
                    CurrentManeuver = (int)Maneuvers.SwerveControlledSkid;
                }
            }
            else if (CurrentManeuver == (int)Maneuvers.BendControlledSkid || CurrentManeuver == (int)Maneuvers.SwerveControlledSkid)
            {
                transform.Translate(SkidVector * skidParam, Space.World);
            }
            ManeuverState++;
        }

        private void JTurn(float jTurnParam) //jTurnParam = +-1 indicates the direction of rotation
        {
            if (ManeuverState == 0)
            {
                Move(1);
                SkidVector = transform.up;
                JTurnBend(jTurnParam);
                CurrentManeuver = (int)Maneuvers.JTurn;
            }
            else if (CurrentManeuver == (int)Maneuvers.JTurn)
            {
                transform.Translate(SkidVector * GridSize, Space.World);
                JTurnBend(-jTurnParam);
            }
            ManeuverState++;
        }

        private void JTurnBend(float jTurnParam)
        {
            Fishtail(jTurnParam * 90f);
        }

        private void TStop(float tStopParam) //tStopParam = +-1 indicates the direction of rotation
        {
            if (ManeuverState == 0)
            {
                Move(1);
                SkidVector = transform.up;
                transform.Rotate(0, 0, tStopParam * 90f);
                CurrentManeuver = (int)Maneuvers.TStop;
            }
            else if (CurrentManeuver == (int)Maneuvers.TStop)
            {
                transform.Translate(SkidVector * GridSize, Space.World);
            }
            ManeuverState++;
            TStopDeccelerate();
        }

        private void TStopDeccelerate()
        {
            if (CurrentSpeed < 20)
                CurrentSpeed = 0;
            else
                CurrentSpeed -= 20;
        }

        private void Pivot(float bendAngle)
        {
            transform.Translate(transform.up * GridSize * 0.25f, Space.World);
            Vector3[] cornerPoints = GetCorners();
            Vector3 rotationPoint;
            if (bendAngle > 0)
                rotationPoint = cornerPoints[3];
            else
                rotationPoint = cornerPoints[2];
            transform.RotateAround(rotationPoint, transform.forward, bendAngle);
            CurrentManeuver = (int)Maneuvers.Pivot;
            ManeuverState = 2;
        }

        private void Fishtail(float bendAngle)
        {
            Vector3[] cornerPoints = GetCorners();
            Vector3 rotationPoint;
            if (bendAngle > 0)
                rotationPoint = cornerPoints[0];
            else
                rotationPoint = cornerPoints[1];
            transform.RotateAround(rotationPoint, transform.forward, bendAngle);
        }

        private void Accelerate(int acceleration)
        {
            CurrentSpeed += acceleration;
            AccelerationPerformed = true;
        }

        private void ExecuteManeuver(int maneuver, float maneuverParam)
        {
            switch (maneuver)
            {
                case (int)Maneuvers.Bend:
                    Bend(maneuverParam);
                    break;
                case (int)Maneuvers.Drift:
                    Drift(maneuverParam);
                    break;
                case (int)Maneuvers.Swerve:
                    Swerve(maneuverParam);
                    break;
                case (int)Maneuvers.BendControlledSkid:
                    ControlledSkid(maneuverParam, true);
                    break;
                case (int)Maneuvers.SwerveControlledSkid:
                    ControlledSkid(maneuverParam, false);
                    break;
                case (int)Maneuvers.JTurn:
                    JTurn(maneuverParam);
                    break;
                case (int)Maneuvers.TStop:
                    TStop(maneuverParam);
                    break;
                case (int)Maneuvers.Pivot:
                    Pivot(maneuverParam);
                    break;
            }
        }

        //TODO: implement difficulty levels and boundaries for maneuvers
        private void PhaseUpdate(int maneuver, float maneuverParam, float maneuverDist, bool acceleartionFlag, int acceleration)
        {
            CurrentDirection = System.Math.Sign(CurrentSpeed);
            float dist = MovementChart[System.Math.Abs(CurrentSpeed) / 5][Phase];
            if (acceleartionFlag && !AccelerationPerformed)
                Accelerate(acceleration);
            if (maneuverDist > dist)
                maneuverDist = dist;
            Move(maneuverDist);
            dist -= maneuverDist;
            if (dist >= 1 && ManeuverState < 2 && maneuver > -1)
                ExecuteManeuver(maneuver, maneuverParam);
            Move(dist);
            if (Phase < 4)
                Phase++;
            else
            {
                Phase = 0;
                AccelerationPerformed = false;
                ManeuverState = 0;
                CurrentManeuver = -1;
                Turn++;
            }
        }

        

        public void ParsePhaseInput()
        {
            var values = GameObject.Find("InputField").GetComponent<InputField>().text.Split(',');
            PhaseUpdate(int.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]), bool.Parse(values[3]), int.Parse(values[4]));
        }

        // Use this for initialization
        void Start()
        {
            CurrentHandlingClass = HandlingClass;
            LoadTable<float>("MovementChart.csv", ',', MovementChart, 60, 5);
            //LoadTable<int>("ControlTable.csv", ',', ControlTable, 30, 15);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}

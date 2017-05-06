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
        private int CurrentHandlingClass;
        private int Phase = 0;
        private int Turn = 0;
        private bool AccelerationPerformed = false;
        private int ManeuverState = 0;              //0 - not performed, 1 - middle phase, 2 - final phase
                                                    //not all maneuvers have the middle phase
        private int CurrentManeuver = -1;
        private Vector3 SkidVector;                 //used only for skid maneuver

        private float[][] MovementChart = new float[60][];

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

        private void LoadMoavementChart(string filename, char separator)
        {
            using (var fs = File.OpenRead(filename))
            using (var reader = new StreamReader(fs))
            {
                for (int i = 0; i < 60; i++)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(separator);
                    MovementChart[i] = new float[5];
                    for (int j = 0; j < 5; j++)
                    {
                        MovementChart[i][j] = float.Parse(values[j]);
                    }
                }
            }
        }

        public void Move()
        {
            var v = transform.up * GridSize * MovementChart[CurrentSpeed / 5][Phase];
            //Debug.Log(v);
            transform.Translate(v, Space.World);
        }

        public void OneMove()
        {
            var v = transform.up * GridSize;
            transform.Translate(v, Space.World);
        }

        public void Drift(float driftOffset)
        {
            OneMove();
            transform.Translate(driftOffset, 0, 0);
            CurrentManeuver = (int)Maneuvers.Drift;
            ManeuverState = 2;
        }

        public void Swerve(float bendAngle)
        {
            transform.Translate(Mathf.Sign(bendAngle) * 0.25f * GridSize, 0, 0);
            Bend(bendAngle);
            CurrentManeuver = (int)Maneuvers.Swerve;
            ManeuverState = 2;
        }

        public void Bend(float bendAngle)
        {
            OneMove();
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

        public void ControlledSkid(float skidParam, bool bend)
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

        public void JTurn(float jTurnParam) //jTurnParam = +-1 indicates the direction of rotation
        {
            if (ManeuverState == 0)
            {
                OneMove();
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

        public void JTurnBend(float jTurnParam)
        {
            Fishtail(jTurnParam * 90f);
        }

        public void TStop(float tStopParam) //tStopParam = +-1 indicates the direction of rotation
        {
            if (ManeuverState == 0)
            {
                OneMove();
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

        public void TStopDeccelerate()
        {
            if (CurrentSpeed < 20)
                CurrentSpeed = 0;
            else
                CurrentSpeed -= 20;
        }

        public void Pivot(float bendAngle)
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

        public void Fishtail(float bendAngle)
        {
            Vector3[] cornerPoints = GetCorners();
            Vector3 rotationPoint;
            if (bendAngle > 0)
                rotationPoint = cornerPoints[0];
            else
                rotationPoint = cornerPoints[1];
            transform.RotateAround(rotationPoint, transform.forward, bendAngle);
        }

        public void Accelerate(int acceleration)
        {
            CurrentSpeed += acceleration;
            AccelerationPerformed = true;
        }

        //TODO: implement difficulty levels and boundaries for maneuvers
        public void PhaseUpdate(int maneuver, float maneuverParam, bool acceleartionFlag, int acceleration)
        {
            if (acceleartionFlag && !AccelerationPerformed)
                Accelerate(acceleration);
            if (MovementChart[CurrentSpeed / 5][Phase] >= 1 && ManeuverState < 2)
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
                    default:
                        Move();
                        break;
                }
            else
                Move();
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
            PhaseUpdate(int.Parse(values[0]), float.Parse(values[1]), bool.Parse(values[2]), int.Parse(values[3]));
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
}

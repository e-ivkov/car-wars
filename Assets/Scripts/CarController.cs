using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
namespace CarWars
{
    enum Maneuvers { Bend, Drift, Swerve, BendControlledSkid, SwerveControlledSkid, JTurn, TStop, Fishtail, Pivot, }
    enum SideNames { Front, Top, Left, Right, Underbody, Back }
    enum Corners { TopLeft, TopRight, BtmLeft, BtmRight }

    public class CarController : MonoBehaviour
    {

        public int Weight
        {
            get
            {
                int weight = CarBody.Weight + CarPowerPlant.Weight;
                weight += 4 * TireType.Weight;
                foreach (int n in Sides)
                {
                    weight += n * CarBody.ArmorWeight;
                }
                foreach (var w in Weapons)
                {
                    weight += w.Weight;
                    weight += w.AmmoWeight * w.MaxAmmoCapacity;
                }
                return weight;
            }
        }
        public int Cost;
        public int HandlingClass;
        public int MaxAcceleration
        {
            get
            {
                if (CarPowerPlant.PowerFactors < Weight / 3)
                    return 0;
                else if (CarPowerPlant.PowerFactors < Weight / 2)
                    return 5;
                else if (CarPowerPlant.PowerFactors < Weight)
                    return 10;
                else
                    return 15;
            }
        }
        public int TopSpeed
        {
            get
            {
                return 360 * CarPowerPlant.PowerFactors / (CarPowerPlant.PowerFactors + Weight);
            }
        }
        public float GridSize;
        [HideInInspector]
        public int[] Tires = new int[4];
        [HideInInspector]
        public int[] Sides = new int[6];            //F, T, L, R, U, B
        public int SideMaxDP;
        public Body CarBody;
        public Tire TireType;
        public PowerPlant CarPowerPlant;
        public ProjectileWeapon[] Weapons;
        public Vector2[] WeaponOrientaions;
        private ActiveCarPart ActivePowerPlant;
        public ActiveWeapon[] ActiveWeapons;

        public bool GameLost
        {
            get
            {
                return !Drivable || (!ActivePowerPlant.Active && System.Math.Abs(CurrentSpeed) < 5);
            }
        }

        public bool Drivable
        {
            get
            {
                return drivable;
            }
        }
        public float DamageModifier
        {
            get
            {
                if (Weight <= 2000)
                {
                    return 1 / 3f;
                }
                else if (Weight <= 4000)
                {
                    return 2 / 3f;
                }
                else
                {
                    return (float)System.Math.Ceiling(Weight / 4000f) - 1;
                }
            }
        }

        private bool drivable = true;
        public int CurrentSpeed = 0;
        public float CurrentReward { get; set; }
        private float currentDirection = 1;
        public float CurrentDirection
        {
            get
            {
                return currentDirection;
            }
        }
        private int currentHandlingClass;
        public int CurrentHandlingClass
        {
            get
            {
                return currentHandlingClass;
            }
            set
            {
                if (value > HandlingClass)
                    currentHandlingClass = HandlingClass;
                else if (value < -6)
                    currentHandlingClass = -6;
                else
                    currentHandlingClass = value;
            }
        }
        private int Phase = 0;
        private int Turn = 0;
        private bool AccelerationPerformed = false;
        private int ManeuverState = 0;              //0 - not performed, 1 - middle phase, 2 - final phase
                                                    //not all maneuvers have the middle phase
        private int CurrentManeuver = -1;

        private Vector3 BeforeManeuverVector;       //used for crash table results
        private bool InCrash = false;
        delegate void Del();
        private int CrashResultIndex = -1;
        private Del[] CrashResults;
        private int RollParam = 0;
        private int[] RollSideIndexes = new int[4] { 2, 1, 3, 4 };

        private float[][] MovementChart = new float[60][];
        private int[][] ControlTable = new int[30][];
        private float[][] TemporarySpeedTable = new float[21][];

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

        public void DamageSide(int i, int damage)
        {
            if (Sides[i] > 0)
            {
                if (Sides[i] > damage)
                    Sides[i] -= damage;
                else
                    Sides[i] = 0;
            }
            else
            {
                ActivePowerPlant.DP -= damage;
            }
        }

        public void FireWeapons()
        {
            foreach (var w in ActiveWeapons)
            {
                if (w.Firing)
                {
                    SetCollidersEnabled(false);
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, w.Orientation);
                    SetCollidersEnabled(true);
                    if (hit.collider != null && RollADice(2) >= ((ProjectileWeapon)w.CarPartType).ToHit)
                    {
                        int side = -1;
                        switch (hit.collider.gameObject.name)
                        {
                            case "FrontSide":
                                side = (int)SideNames.Front;
                                break;
                            case "RearSide":
                                side = (int)SideNames.Back;
                                break;
                            case "LeftSide":
                                side = (int)SideNames.Left;
                                break;
                            case "RightSide":
                                side = (int)SideNames.Right;
                                break;
                        }
                        if (side >= 0)
                            hit.collider.GetComponentInParent<CarController>().DamageSide(side,
                                    RollADice(((ProjectileWeapon)w.CarPartType).DamageDiceNumber));
                    }
                    w.Ammo--;
                }
            }
        }

        /// <param name="n"> number of dice </param>
        private int RollADice(int n)
        {
            int r = 0;
            for (int i = 0; i < n; i++)
                r += Random.Range(1, 7);
            return r;
        }

        private int ComputeRamDamage(int speed)
        {
            if (speed > 25)
                return RollADice(speed / 5 - 5);
            else
                return RollADice(1);
        }

        private void Move(float dist)
        {
            var v = transform.up * GridSize * dist * CurrentDirection;
            //Debug.Log(v);
            SetCollidersEnabled(false);
            RaycastHit2D hit = Physics2D.Raycast(transform.position, transform.up * CurrentDirection, v.magnitude + 1);
            Debug.DrawRay(transform.position, v, new Color(255, 0, 0), 10);
            if (hit.collider != null) //collision detected
            {
                if (hit.collider.CompareTag("Killzone"))
                {
                    Reset();
                    CurrentReward = -500;
                }
                else
                {
                    var v1 = transform.up * CurrentDirection * (hit.distance - 1);
                    transform.Translate(v1);
                    bool canMove = HandleCollision(hit.collider);
                    if (canMove)
                        transform.Translate(v - v1);
                    CurrentReward = -100;
                }
            }
            else
            {
                transform.Translate(v, Space.World);
                CurrentReward = System.Math.Abs(CurrentSpeed / 5);
            }
            SetCollidersEnabled(true);
        }

        private bool HandleCollision(Collider2D collision) //returns true if vehicle can continue its movement
        {
            bool flag = false;
            if (collision.gameObject.tag == "CarSide")
            {
                CarController opponent = collision.GetComponentInParent<CarController>();
                int oldSpeed = CurrentSpeed;
                int opponentOldSpeed = opponent.CurrentSpeed;


                switch (collision.gameObject.name)
                {
                    case "RightSide":
                        HandleTBone(opponent, false);
                        if (System.Math.Abs(CurrentSpeed) > 0)
                        {
                            PushConformingOpponent((int)Corners.TopRight, 1, collision, opponent.transform);
                            flag = true;
                        }
                        break;
                    case "LeftSide":
                        HandleTBone(opponent, true);
                        if (System.Math.Abs(CurrentSpeed) > 0)
                        {
                            PushConformingOpponent((int)Corners.TopLeft, -1, collision, opponent.transform);
                            flag = true;
                        }
                        break;
                    case "FrontSide":
                        if (opponent.CurrentDirection > 0)
                        {
                            HandleHeadOn(opponent);
                            if (System.Math.Abs(CurrentSpeed) > 0)
                            {
                                PushConformingOpponent((int)Corners.TopLeft, 1, collision, opponent.transform);
                                flag = true;
                            }
                        }
                        else
                        {
                            HandleRearEnd(opponent);
                            if (DamageModifier > opponent.DamageModifier)
                            {
                                PushConformingOpponent((int)Corners.TopLeft, -1, collision, opponent.transform);
                                flag = true;
                            }
                        }
                        break;
                    case "RearSide":
                        if (opponent.CurrentDirection > 0)
                        {
                            HandleRearEnd(opponent);
                            if (DamageModifier > opponent.DamageModifier)
                            {
                                PushConformingOpponent((int)Corners.BtmRight, 1, collision, opponent.transform);
                                flag = true;
                            }
                        }
                        else
                        {
                            HandleHeadOn(opponent);
                            if (System.Math.Abs(CurrentSpeed) > 0)
                            {
                                PushConformingOpponent((int)Corners.BtmRight, 1, collision, opponent.transform);
                                flag = true;
                            }
                        }
                        break;
                    default:
                        flag = true;
                        break;
                }


                opponent.CurrentHandlingClass -= (int)System.Math.Ceiling((opponentOldSpeed - opponent.CurrentSpeed) / 10f);
                opponent.ControllRoll(1);
            }
            else
            {
                CurrentSpeed = 0;
                transform.Translate(-transform.up * currentDirection * GridSize);
            }
            ControllRoll(1);
            return flag;
        }

        private void HandleTBone(CarController opponent, bool left)
        {
            int collisionSpeed = CurrentSpeed;
            int ram = ComputeRamDamage(collisionSpeed);
            if (CurrentDirection > 0)
                DamageSide((int)SideNames.Front, (int)System.Math.Round(ram * opponent.DamageModifier));
            else
                DamageSide((int)SideNames.Back, (int)System.Math.Round(ram * opponent.DamageModifier));
            if (left)
                opponent.DamageSide((int)SideNames.Left, (int)System.Math.Round(ram * DamageModifier));
            else
                opponent.DamageSide((int)SideNames.Right, (int)System.Math.Round(ram * DamageModifier));
            CurrentSpeed = (int)System.Math.Round(CurrentSpeed * TemporarySpeedTable[(int)DamageModifier][(int)opponent.DamageModifier] / 5) * 5;

        }

        private void HandleHeadOn(CarController opponent)
        {
            int collisionSpeed = CurrentSpeed + opponent.CurrentSpeed;
            int ram = ComputeRamDamage(collisionSpeed);
            if (CurrentDirection > 0)
                DamageSide((int)SideNames.Front, (int)System.Math.Round(ram * opponent.DamageModifier));
            else
                DamageSide((int)SideNames.Back, (int)System.Math.Round(ram * opponent.DamageModifier));
            if (opponent.CurrentDirection < 0)
                opponent.DamageSide((int)SideNames.Back, (int)System.Math.Round(ram * DamageModifier));
            else
                opponent.DamageSide((int)SideNames.Front, (int)System.Math.Round(ram * DamageModifier));
            int tempSpeed1 = (int)System.Math.Round(CurrentSpeed * TemporarySpeedTable[(int)DamageModifier][(int)opponent.DamageModifier] / 5) * 5;
            int tempSpeed2 = (int)System.Math.Round(opponent.CurrentSpeed * TemporarySpeedTable[(int)opponent.DamageModifier][(int)DamageModifier] / 5) * 5;
            if (tempSpeed1 > tempSpeed2)
            {
                CurrentSpeed = tempSpeed1 - tempSpeed2;
                opponent.CurrentSpeed = 0;
            }
            else
            {
                opponent.CurrentSpeed = tempSpeed2 - tempSpeed1;
                CurrentSpeed = 0;
            }
        }

        private void HandleRearEnd(CarController opponent)
        {
            int collisionSpeed = System.Math.Abs(CurrentSpeed - opponent.CurrentSpeed);
            int ram = ComputeRamDamage(collisionSpeed);
            if (CurrentDirection > 0)
                DamageSide((int)SideNames.Front, (int)System.Math.Round(ram * opponent.DamageModifier));
            else
                DamageSide((int)SideNames.Back, (int)System.Math.Round(ram * opponent.DamageModifier));
            if (opponent.CurrentDirection > 0)
                opponent.DamageSide((int)SideNames.Back, (int)System.Math.Round(ram * DamageModifier));
            else
                opponent.DamageSide((int)SideNames.Front, (int)System.Math.Round(ram * DamageModifier));
            int tempSpeed1 = (int)System.Math.Round(CurrentSpeed * TemporarySpeedTable[(int)DamageModifier][(int)opponent.DamageModifier] / 5) * 5;
            int tempSpeed2 = (int)System.Math.Round(opponent.CurrentSpeed * TemporarySpeedTable[(int)opponent.DamageModifier][(int)DamageModifier] / 5) * 5;
            CurrentSpeed = tempSpeed1 + tempSpeed2;
            opponent.CurrentSpeed = CurrentSpeed;
        }

        private void PushConformingOpponent(int corner, float angle, Collider2D collision, Transform opponent)
        {
            var v = transform.up * CurrentDirection;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, transform.up * CurrentDirection);
            Vector3[] corners = GetCorners();
            while (hit.collider == collision)
            {
                opponent.transform.RotateAround(corners[corner], opponent.transform.forward, angle);
                hit = Physics2D.Raycast(transform.position, transform.up * CurrentDirection);
            }
        }

        public void SetCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).GetComponent<Collider2D>().enabled = enabled;
            }
            transform.GetComponent<Collider2D>().enabled = enabled;
        }

        private void Drift(float driftOffset)
        {
            if (System.Math.Abs(driftOffset) > 0.5f || System.Math.Abs(driftOffset) < 0.25f)
                throw new System.ArgumentException("Absolute value of driftOffset should be in range from 0.25 to 0.5", "driftOffset");
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
            if (System.Math.Abs(bendAngle) > 90)
                throw new System.ArgumentException("Bend angle can not be more then 90", "bendAngle");
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
                if (skidParam > 1 || skidParam < 0.25f)
                    throw new System.ArgumentException("SkidParam on the second state should be in range from 0 to 1", "skidParam");
                transform.Translate(BeforeManeuverVector * skidParam, Space.World);
            }
            ManeuverState++;
        }

        private void JTurn(float jTurnParam) //jTurnParam = +-1 indicates the direction of rotation
        {
            if (System.Math.Abs(jTurnParam) != 1)
                throw new System.ArgumentException("JTurnParam can be only 1 or -1", "jTurnParam");
            if (System.Math.Abs(CurrentSpeed) < 20 || System.Math.Abs(CurrentSpeed) > 35)
                throw new System.InvalidOperationException("J-Turn can only be performed if speed is between 20 and 35 mph");
            if (ManeuverState == 0)
            {
                Move(1);
                JTurnBend(jTurnParam);
                CurrentManeuver = (int)Maneuvers.JTurn;
                for (int i = 0; i < 4; i++)
                    if (Tires[i] > 0)
                        Tires[i]--;             //TODO: check damage
            }
            else if (CurrentManeuver == (int)Maneuvers.JTurn)
            {
                transform.Translate(BeforeManeuverVector * GridSize, Space.World);
                JTurnBend(-jTurnParam);
            }
            ManeuverState++;
            DeccelerateUncontrolled(20);
        }

        private void JTurnBend(float jTurnParam)
        {
            Fishtail(jTurnParam * 90f);
        }

        private void TStop(float tStopParam) //tStopParam = +-1 indicates the direction of rotation
        {
            if (System.Math.Abs(tStopParam) != 1)
                throw new System.ArgumentException("TStopParam can be only 1 or -1", "tStopParam");
            if (CurrentSpeed < 20 || CurrentSpeed > 35)
                throw new System.InvalidOperationException("T-Stop can only be performed if speed is between 20 and 35 mph");
            if (ManeuverState == 0)
            {
                Move(1);
                transform.Rotate(0, 0, tStopParam * 90f);
                CurrentManeuver = (int)Maneuvers.TStop;
            }
            else if (CurrentManeuver == (int)Maneuvers.TStop)
            {
                transform.Translate(BeforeManeuverVector * GridSize, Space.World);
            }
            ManeuverState++;
            DeccelerateUncontrolled(20);
            for (int i = 0; i < 4; i++)
                if (Tires[i] > 0)
                    Tires[i]--;             //TODO: check damage
        }

        private void Deccelerate(int a)
        {
            int tireDamage = 0;
            int d = 0;
            switch (a)
            {
                case 15:
                    d = 1;
                    break;
                case 20:
                    d = 2;
                    break;
                case 25:
                    d = 3;
                    break;
                case 30:
                    d = 5;
                    break;
                case 35:
                    d = 7;
                    tireDamage = 2;
                    break;
            }
            if (a >= 40)
            {
                d = a / 5 + 2;
                tireDamage = RollADice(1) + 3 * (a / 5 - 8);
            }
            for (int i = 0; i < 4; i++)
                if (Tires[i] > 0)
                    Tires[i] -= tireDamage;
            CurrentHandlingClass -= d;
            DeccelerateUncontrolled(a);
            ManeuverState = 2;
            if (d > 0)
            {
                ControllRoll(d);
            }
        }

        private void DeccelerateUncontrolled(int a)
        {
            if (System.Math.Abs(CurrentSpeed) < a)
                CurrentSpeed = 0;
            else
                CurrentSpeed -= a * (int)CurrentDirection;
        }

        private void Pivot(float bendAngle)
        {
            if (System.Math.Abs(bendAngle) > 90)
                throw new System.ArgumentException("Bend angle can not be more then 90", "bendAngle");
            if (System.Math.Abs(CurrentSpeed) > 5)
                throw new System.InvalidOperationException("Speed should be no more than 5 mph to perform pivot");
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
            if (acceleration % 5 != 0)
                throw new System.ArgumentException("Acceleration should be divisible by 5", "acceleration");
            if (System.Math.Abs(acceleration) > MaxAcceleration)
                throw new System.ArgumentException("Can not accelerate higher than MaxAcceleration", "acceleration");
            if (CurrentSpeed == 0 || System.Math.Sign(acceleration) == System.Math.Sign(CurrentSpeed))
            {
                if (ActivePowerPlant.Active)
                {
                    CurrentSpeed += acceleration;
                    AccelerationPerformed = true;
                }
            }
            else
                Deccelerate(System.Math.Abs(acceleration));
        }


        private void ExecuteManeuver(int maneuver, float maneuverParam)
        {
            BeforeManeuverVector = transform.up * CurrentDirection;
            int d = 0;
            switch (maneuver)
            {
                case (int)Maneuvers.Bend:
                    d = (int)System.Math.Ceiling(maneuverParam / 15);
                    Bend(maneuverParam);
                    break;
                case (int)Maneuvers.Drift:
                    if (System.Math.Abs(maneuverParam) <= 0.25)
                        d = 1;
                    else
                        d = 3;
                    Drift(maneuverParam);
                    break;
                case (int)Maneuvers.Swerve:
                    d = (int)System.Math.Ceiling(maneuverParam / 15);
                    Swerve(maneuverParam);
                    break;
                case (int)Maneuvers.BendControlledSkid:
                    d = (int)System.Math.Ceiling(maneuverParam / 15) + 2;
                    ControlledSkid(maneuverParam, true);
                    break;
                case (int)Maneuvers.SwerveControlledSkid:
                    d = (int)System.Math.Ceiling(maneuverParam / 15) + 2;
                    ControlledSkid(maneuverParam, false);
                    break;
                case (int)Maneuvers.JTurn:
                    d = 7;
                    for (int i = 0; i < 4; i++)     //TODO: check damage
                        Tires[i]--;
                    JTurn(maneuverParam);
                    break;
                case (int)Maneuvers.TStop:
                    d = System.Math.Abs(CurrentSpeed) / 10;
                    TStop(maneuverParam);
                    break;
                case (int)Maneuvers.Pivot:
                    Pivot(maneuverParam);
                    break;
            }
            CurrentHandlingClass -= d;
            if (d > 0)
            {
                ControllRoll(d);
            }
        }

        public void ControllRoll(int d)
        {
            int c = ControlTable[(int)System.Math.Ceiling(System.Math.Abs(CurrentSpeed) / 10f)][7 - CurrentHandlingClass];
            if (RollADice(1) < c)
                StartCrash(ControlTable[(int)System.Math.Ceiling(System.Math.Abs(CurrentSpeed) / 10f)][14], d);
        }

        private void StartCrash(int crashModifier, int d)
        {
            int roll = RollADice(2) + crashModifier + d - 3;
            if (roll <= 16)
            {
                CrashResultIndex = (int)System.Math.Floor((roll - 1) / 2f);
            }
            else
            {
                CrashResultIndex = 7;
            }
            CurrentReward = roll * -15;
            InCrash = true;
            ManeuverState = 0;
            Debug.Log("here!");
        }

        private void Vault()
        {
            Skid(RollADice(1));
            int tireDamage = RollADice(3);
            for (int i = 0; i < 4; i++)
                if (Tires[i] > 0)
                    Tires[i] -= tireDamage;
            //TODO: Add collision damage upon landing
            CrashResultIndex = 5;
            Debug.Log("Vault");
        }

        private void BurningRoll()
        {
            //TODO: Add burning effect
            Roll();
            Debug.Log("BurningRoll");
        }

        private void Roll()
        {
            Skid(1);
            DeccelerateUncontrolled(20);
            transform.rotation = Quaternion.LookRotation(new Vector3(0, 0, 1), BeforeManeuverVector);
            transform.Rotate(0, 0, 90);
            int existingTires = 0;
            for (int i = 0; i < 4; i++)
                if (Tires[i] > 0)
                    existingTires++;
            if (RollParam % 4 == 3 && existingTires > 0)
            {
                for (int i = 0; i < 4; i++)
                    if (Tires[i] > 0)
                        Tires[i]--;
            }
            else
            {
                Sides[RollSideIndexes[RollParam % 4]]--;
            }
            RollParam++;
            if (CurrentSpeed == 0)
            {
                InCrash = false;
                drivable = false;
                if (RollParam % 4 == 3 && existingTires >= 3)
                    drivable = true;
                RollParam = 0;
            }
            Debug.Log("Roll");
        }

        private void Spinout()
        {
            Skid(1);
            DeccelerateUncontrolled(20);
            for (int i = 0; i < 4; i++) //TODO: check damage
                Tires[i]--;
            transform.Rotate(0, 0, 90);
            int c = ControlTable[(int)System.Math.Ceiling(System.Math.Abs(CurrentSpeed) / 10f)][13];
            if (RollADice(1) >= c || CurrentSpeed == 0)
                InCrash = false;
            Debug.Log("Spinout");
        }

        private void SevereSkid()
        {
            Skid(1);
            DeccelerateUncontrolled(20);
            for (int i = 0; i < 4; i++) //TODO: check damage
                Tires[i] -= 2;
            CrashResultIndex = 1;
            Debug.Log("SevereSkid");
        }

        private void ModerateSkid()
        {
            Skid(0.75f);
            DeccelerateUncontrolled(10);
            for (int i = 0; i < 4; i++) //TODO: check damage
                Tires[i]--;
            CrashResultIndex = 0;
            Debug.Log("ModerateSkid");
        }

        private void MinorSkid()
        {
            Skid(0.2f);
            DeccelerateUncontrolled(5);
            InCrash = false;
            Debug.Log("MinorSkid");
        }

        private void TrivialSkid()
        {
            Skid(0.25f);
            InCrash = false;
            Debug.Log("TrivialSkid");
        }

        private void Skid(float dist)
        {
            transform.Translate(BeforeManeuverVector * GridSize * dist, Space.World);
        }

        private void CheckDrivable()
        {
            int existingTires = 0;
            for (int i = 0; i < 4; i++)
                if (Tires[i] > 0)
                    existingTires++;
            if (existingTires > 3)
                drivable = true;
            else
                drivable = false;
        }

        /// <summary>
        /// Method which is called each phase for each car
        /// </summary>
        /// <param name="maneuver"> -1 or Maneuver; If maneuver is 2 state, then it should be called twice </param>
        /// <param name="maneuverParam">usually bendAngle but may differ for various manuevers</param>
        /// <param name="maneuverDist">should be 0 on the second state of maneuver if it's 2 state</param>
        /// <param name="acceleartionFlag">whether accelerate or not</param>
        /// <param name="acceleration"> acceleration % 5 = 0 </param>
        /// <param name="weaponIndex">switches on/off weapon with given index, or does nothing if -1 </param>
        public void PhaseUpdate(int maneuver, float maneuverParam, float maneuverDist, bool acceleartionFlag, int acceleration, int weaponIndex)
        {
            CheckDrivable();
            if (InCrash)
            {
                CrashResults[CrashResultIndex]();
                CurrentReward = 0;
            }
            else if (drivable)
            {
                currentDirection = System.Math.Sign(CurrentSpeed);
                float dist = MovementChart[System.Math.Abs(CurrentSpeed) / 5][Phase];
                if (acceleartionFlag && !AccelerationPerformed)
                    Accelerate(acceleration);
                if (maneuverDist > dist)
                    maneuverDist = dist;
                Move(maneuverDist);
                dist -= maneuverDist;
                if (dist >= 1 && ManeuverState == 0 && maneuver > -1)
                    ExecuteManeuver(maneuver, maneuverParam);
                if (ManeuverState == 1 && maneuver > -1)
                    ExecuteManeuver(maneuver, maneuverParam);
                else
                    Move(dist);
                if (!ActivePowerPlant.Active)
                    DeccelerateUncontrolled(5);
                if (weaponIndex > -1 && weaponIndex < ActiveWeapons.Length)
                    ActiveWeapons[weaponIndex].TurnFiringOnOff();
                FireWeapons();
            }
            if (Phase < 4)
                Phase++;
            else
            {
                Phase = 0;
                AccelerationPerformed = false;
                CurrentHandlingClass++;
                if (ManeuverState == 2)
                {
                    ManeuverState = 0;
                    CurrentManeuver = -1;
                }
                Turn++;
            }
            if (GameLost)
            {
                CurrentReward = -500;
                Reset();
            }
        }

        public void Reset()
        {
            transform.rotation = Quaternion.identity;
            transform.position = Vector3.zero;
            CurrentReward = 0;
            CurrentSpeed = 0;
            currentDirection = 1;
            CurrentHandlingClass = HandlingClass;
            ActivePowerPlant = new ActiveCarPart(CarPowerPlant);
            ActiveWeapons = new ActiveWeapon[Weapons.Length];
            drivable = true;
            Phase = 0;
            Turn = 0;
            ManeuverState = 0;
            AccelerationPerformed = false;
            InCrash = false;
            CurrentManeuver = -1;
            RollParam = 0;
            CrashResultIndex = -1;
            for (int i = 0; i < ActiveWeapons.Length; i++)
            {
                ActiveWeapons[i] = new ActiveWeapon(Weapons[i], WeaponOrientaions[i]);
            }
            for (int i = 0; i < Tires.Length; i++)
            {
                Tires[i] = TireType.MaxDP;
            }
            for (int i = 0; i < Sides.Length; i++)
            {
                Sides[i] = SideMaxDP;
            }
        }

        public void ParsePhaseInput()
        {
            var values = GameObject.Find("InputField").GetComponent<InputField>().text.Split(',');
            PhaseUpdate(int.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]), bool.Parse(values[3]), int.Parse(values[4]), int.Parse(values[5]));
        }

        // Use this for initialization
        void Start()
        {
            LoadTable("MovementChart.csv", ',', MovementChart, 60, 5);
            LoadTable("ControlTable.csv", ',', ControlTable, 30, 15);
            LoadTable("TemporarySpeedTable.csv", ',', TemporarySpeedTable, 21, 21);
            CrashResults = new Del[8] { TrivialSkid, MinorSkid, ModerateSkid, SevereSkid, Spinout, Roll, BurningRoll, Vault };
            Reset();
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}

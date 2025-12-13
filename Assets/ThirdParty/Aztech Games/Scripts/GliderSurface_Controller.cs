using UnityEngine;
using UnityEngine.UI;

namespace AztechGames
{
    public class GliderSurface_Controller : Singleton< GliderSurface_Controller >
    {
        [Header("Control Surfaces")]
        [Tooltip("Left aileron transform")]
        public Transform aileronLeft;

        [Tooltip("Right aileron transform")]
        public Transform aileronRight;

        [Tooltip("Elevator transform")]
        public Transform elevator;

        [Tooltip("Left slats transform")]
        public Transform slatsLeft;

        [Tooltip("Right slats transform")]
        public Transform slatsRight;

        [Header("Surfaces Max Angle")]
        [Tooltip("Maximum angle for aileron movement")]
        public float aileronMaxAngle = 30f;

        [Tooltip("Maximum angle for elevator movement")]
        public float elevatorMaxAngle = 30f;

        [Tooltip("Maximum angle for slats movement")]
        public float slatMaxAngle = 0.22f;

        [Tooltip("Speed of control surface movement")]
        public float surfaceSpeed = 2f;
        public AttitudeHUD attitudeHUD {get;private set;}
        public HeadingHUD headingHUD {get;private set;}
        public BankIndicatorHUD rotator;
        public Glideslope glideslope;
        public Text headingText;
        

        // Control Surfaces Angles
        private float _slatAmount;
        private float _elevatorAmount;
        private float _aileronAmount;

        public float SlatAmount
        {
            get => _slatAmount;
            set => _slatAmount = Mathf.Clamp(value, 0f, slatMaxAngle);
        }
        public float ElevatorAmount
        {
            get => Mathf.Clamp(_elevatorAmount, -elevatorMaxAngle, elevatorMaxAngle);
            set => _elevatorAmount = value;
        }
        public float AileronAmount
        {
            get => Mathf.Clamp(_aileronAmount, -aileronMaxAngle, aileronMaxAngle);
            set => _aileronAmount = value;
        }
        private void Start()
        {
            if (attitudeHUD == null)
            {
                attitudeHUD = FindObjectOfType<AttitudeHUD>();
            }
            if (headingHUD == null)
            {
                headingHUD = FindObjectOfType<HeadingHUD>();
            }
            if (rotator == null)
            {
                rotator = FindObjectOfType<BankIndicatorHUD>();

            }
            if (glideslope == null)
            {
                glideslope = FindObjectOfType<Glideslope>();
            }
            // if (airspeedHUD == null)
            // {
            //     airspeedHUD = FindObjectOfType<AirspeedHUD>();
            // }
        }
        public virtual void GetInputs()
        {
            // Apply the input values to the control surfaces
            AileronController(Input.GetAxis("Horizontal"));
            ElevatorController(Input.GetAxis("Vertical"));
            SlatController();

            // Reset Positions
            if (Input.GetKeyDown(KeyCode.R))
            {
                AileronAmount = 0;
                ElevatorAmount = 0;
                SlatAmount = 0;
            }
        }

        void AileronController(float input)
        {
            AileronAmount += input * surfaceSpeed;
            attitudeHUD.UpdateRoll(AileronAmount);

            aileronLeft.localRotation = Quaternion.Euler(Vector3.forward * (_aileronAmount - 90f) + Vector3.down * 90);
            aileronRight.localRotation = Quaternion.Euler(Vector3.back * (_aileronAmount + 90f) + Vector3.down * 90);
        }

        void ElevatorController(float input)
        {
            ElevatorAmount += input * surfaceSpeed;
            attitudeHUD.UpdatePitch(ElevatorAmount);

            elevator.localRotation = Quaternion.Euler(Vector3.left * _elevatorAmount);
        }

        void SlatController()
        {
            // Increase slat angle when pressing B, decrease when pressing V
            if (Input.GetKey(KeyCode.B))
            {
                SlatAmount += Time.deltaTime * surfaceSpeed;
            }
            else if (Input.GetKey(KeyCode.V))
            {
                SlatAmount -= Time.deltaTime * surfaceSpeed;
            }

            // Clamp the slat amount between 0 and slatMaxAngle
            SlatAmount = Mathf.Clamp(SlatAmount, 0f, slatMaxAngle);

            slatsLeft.localPosition = Vector3.forward * _slatAmount;
            slatsRight.localPosition = Vector3.forward * _slatAmount;
        }

        public void PlaneRotations()
        {
            transform.Rotate(new Vector3(ElevatorAmount / surfaceSpeed, 0f, -AileronAmount) * Time.deltaTime);
        }
        //更新Heading
        private void Update()
        {
            headingHUD.UpdateHeading(transform.eulerAngles.y);
            rotator.UpdateBank(-transform.eulerAngles.z);
            glideslope.UpdateGlideslope(ElevatorAmount);
        }
    
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;
using UnityEngine.InputSystem.Controls;
using System.Collections.Concurrent;
using AOT;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Scripting;
using System.Text;
using Unity.Plastic.Newtonsoft.Json;


[assembly : AlwaysLinkAssembly]
namespace MoreStories.GyroTools
{
    public static class GyroOverride
    {

        #region gyro_reader_methods

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        const string imu_library = "imu_reader";
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        const string imu_library = "libimu_reader";
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ControllerSensorCallback(int controllerIndex, float x, float y, float z);

        [DllImport(imu_library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void register_gyro_callback(ControllerSensorCallback callback);
        
        [DllImport(imu_library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void register_accel_callback(ControllerSensorCallback callback);

        [DllImport(imu_library, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool set_controller_imu_state(int controller_index, bool is_enabled);

        #region optional_polling_rate_methods

        // These two methods can allow you to change the SDL's thread's polling rate for performance or compatibility reasons
        [DllImport(imu_library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void change_polling_rate(float polling_rate);

        [DllImport(imu_library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void start_variable_rate_sdl_loop();

        #endregion

        [DllImport(imu_library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void start_sdl_loop();

        [DllImport(imu_library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void stop_sdl_loop();

        #endregion

        #region internal_types

        struct MotionControls
        {
            Vector3Control[] imus;
            public Gamepad owner {get; private set;}

            public Vector3Control gyroscope     => this[ImuType.Gyroscope];
            public Vector3Control accelerometer => this[ImuType.Accelerometer];

            public Vector3Control this[ImuType type]
            {
                get         => imus[(int)type];
                private set => imus[(int)type] = value;
            } 

            public MotionControls(Gamepad owner, Vector3Control gyroscope, Vector3Control accelerometer)
            {
                this.owner = owner;
                imus = new Vector3Control[(int)ImuType.Count];

                this[ImuType.Gyroscope]     = gyroscope;
                this[ImuType.Accelerometer] = accelerometer;
            }

        }
        public struct ImuReading
        {
            public Vector3 value       {get; private set;}
            public int controllerIndex {get; private set;}
            
            public ImuReading(int controllerIndex, Vector3 value)
            {
                this.controllerIndex = controllerIndex;
                this.value           = value;
            }

            public ImuReading(int controllerIndex, float x, float y, float z)
            {
                this.controllerIndex = controllerIndex;
                value = new Vector3(x, y, z);
            }
        }

        public enum ImuType
        {
            Gyroscope,
            Accelerometer,
            Count = 2
        }
       
        #endregion

        public const string DS4HIDLayoutName = "Dualshock4GamepadHID";
        public const string IMUControlPath   = "IMU";
        public const string GyroControlPath  = IMUControlPath + "/gyro";
        public const string AccelControlPath = IMUControlPath + "/accel";

        static object GamepadWithIMUOverride = new
        {
            name = "GamepadWithIMU",
            extend = "Gamepad",
            controls = new object[]
            {
                new { name = IMUControlPath,   layout = IMUControlPath, synthetic = true, offset = 64 }, //Large offset so that it doesn't conflict with HID values
                new { name = GyroControlPath,  layout = "Vector3",      synthetic = true },
                new { name = AccelControlPath, layout = "Vector3",      synthetic = true }
            }
        };

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        static object Dualshock4HIDOverride = new
        {
            name = "Dualshock4GamepadHIDCustom",
            extend = DS4HIDLayoutName,
            controls = new object[]
            {
                new { name = IMUControlPath,   layout = IMUControlPath }, 
                new { name = GyroControlPath,  format = "VC3S", layout = "Vector3", offset = 13, processors = "ScaleVector3(x=-8,  y=-8,  z=8)"  },
                    new { name = GyroControlPath + "/x",  format = "SHRT", offset = 0},
                    new { name = GyroControlPath + "/y",  format = "SHRT", offset = 2},
                    new { name = GyroControlPath + "/z",  format = "SHRT", offset = 4},
                new { name = AccelControlPath, format = "VC3S", layout = "Vector3", offset = 19, processors = "ScaleVector3(x=-38, y=-38, z=38)" },
                    new { name = AccelControlPath + "/x",  format = "SHRT", offset = 0},
                    new { name = AccelControlPath + "/y",  format = "SHRT", offset = 2},
                    new { name = AccelControlPath + "/z",  format = "SHRT", offset = 4}
            }
        };
#endif
        static MotionControls[] motionControls;
        static ConcurrentQueue<ImuReading> gyroReadings  = new ConcurrentQueue<ImuReading>(), 
                                           accelReadings = new ConcurrentQueue<ImuReading>();
        
        static bool LoadImuReading(ImuType imuType, ref ImuReading imuReading) 
        => imuType switch
        {
            ImuType.Gyroscope     => gyroReadings.  TryDequeue(out imuReading),
            ImuType.Accelerometer => accelReadings. TryDequeue(out imuReading),
             _ => false
        };

        static void AddNewIMULayout()
        {
            InputSystem.RegisterLayout<IMUControl>(IMUControlPath);
            InputSystem.RegisterLayoutOverride(JsonConvert.SerializeObject(GamepadWithIMUOverride));
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            InputSystem.RegisterLayoutOverride(JsonConvert.SerializeObject(Dualshock4HIDOverride));
#endif
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void AddImuOverride() => AddNewIMULayout();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ImplementIMU()
        {

#if UNITY_STANDALONE
            AddNewIMULayout();
#endif

            RefreshGamepadControls(null, InputDeviceChange.Added);
            InputSystem.onDeviceChange -= RefreshGamepadControls;
            InputSystem.onDeviceChange += RefreshGamepadControls;

            // For optimal motion sensor performance and hardware compatibility
            // It might be optimal to change the Input System's update rate by updating it manually
            // Otherwise it just runs as quickly as it can which might not be what is desired
            InputSystem.onBeforeUpdate -= FeedImuValues;
            InputSystem.onBeforeUpdate += FeedImuValues;

            start_sdl_loop ();

            register_gyro_callback  (ReadGyro);   
            register_accel_callback (ReadAccel);

            Application.quitting += OnQuit;

        }

        static void FeedImuValues()
        {
            ImuReading imu = new ImuReading();
            DequeueImuValues(ImuType.Gyroscope,     ref imu);
            DequeueImuValues(ImuType.Accelerometer, ref imu);

        }

        static void DequeueImuValues(ImuType type, ref ImuReading imuReading)
        {
           
            while(LoadImuReading(type, ref imuReading))
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

                StateEvent.From(motionControls[imuReading.controllerIndex].owner, out var eventPtr);
                motionControls[imuReading.controllerIndex][type].WriteValueIntoEvent(imuReading.value, eventPtr);
                InputSystem.QueueEvent(eventPtr);

#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
                           
                InputSystem.QueueDeltaStateEvent(motionControls[imuReading.controllerIndex][type], imuReading.value);
#endif                 
            }
        }

        static void OnQuit()
        {
            stop_sdl_loop();
            InputSystem.onDeviceChange -= RefreshGamepadControls;
            InputSystem.onBeforeUpdate -= FeedImuValues;

        }
        
        /// According to the SDL wiki SDL uses a right hand coordinate system where Y is up
        /// Thus positive rotations are those seen from the positive side of an axis going counter clockwise
        /// 
        /// Unity uses a left hand coordinate system where Y is up
        /// Thus positive rotations are those seen from the positive side of an axis going clockwise
        /// 
        /// Thus we translate the values from SDL to be in line with the Unity standard
        [MonoPInvokeCallback (typeof(ControllerSensorCallback))]
        static void ReadGyro  (int controllerIndex, float x, float y, float z) => gyroReadings.  Enqueue(new ImuReading(controllerIndex, -x, -y, z));

        [MonoPInvokeCallback (typeof(ControllerSensorCallback))]
        static void ReadAccel (int controllerIndex, float x, float y, float z) => accelReadings. Enqueue(new ImuReading(controllerIndex,  x,  y, z));

        static void RefreshGamepadControls(InputDevice device, InputDeviceChange change)
        {
            if(change == InputDeviceChange.Added || change == InputDeviceChange.Disconnected)
            {
                var gamepads = Gamepad.all;
                motionControls = new MotionControls[gamepads.Count];

                for (int i = 0; i < gamepads.Count; i++)
                {
                    if(gamepads[i].layout == "Dualshock4GamepadHID")
                    {
                        set_controller_imu_state(i, false);
                        continue;
                    } 
                    var gyro  = gamepads[i].TryGetChildControl<Vector3Control>(GyroControlPath);
                    var accel = gamepads[i].TryGetChildControl<Vector3Control>(AccelControlPath);

                    if (gyro == null || accel == null)
                    {
                        Debug.LogError("Motion sensor controls are missing from Input set, check if layout override is working properly");
                        return;
                    }
                    motionControls[i] = new MotionControls(gamepads[i], gyro, accel);
                }
            }
            
        }

    }
}



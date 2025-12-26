using UnityEngine;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;
using UnityEngine.InputSystem.Controls;
using System.Collections.Concurrent;
using AOT;
using UnityEngine.InputSystem.LowLevel;

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
       
        const float SdlPollingRateLimit = 250f;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ImplementIMU()
        {
            RefreshGamepadControls(null, InputDeviceChange.Added);
            InputSystem.onDeviceChange -= RefreshGamepadControls;
            InputSystem.onDeviceChange += RefreshGamepadControls;

            // For optimal motion sensor performance and hardware compatibility
            // It is recommended to change the Input System's update rate by updating it manually
            // Otherwise it just runs as quickly as it can which might not be what is desired
            InputSystem.onBeforeUpdate -= FeedImuValues;
            InputSystem.onBeforeUpdate += FeedImuValues;

            stop_sdl_loop  ();
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
                if(motionControls?.Length > 0)
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
                    var gyro  = gamepads[i].TryGetChildControl<Vector3Control>("Gyroscope");
                    var accel = gamepads[i].TryGetChildControl<Vector3Control>("Accelerometer");

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



#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

// Revise this code if building doesn't work
namespace MoreStories.GyroTools.Editor
{

    [InitializeOnLoad]
    public class AddGamepadWithIMU
    {
        const string GamepadWithIMUOverride = @"{
        ""name"": ""GamepadWithIMU"",
        ""extend"": ""Gamepad"",
        ""controls"": [
        {""name"": ""Gyroscope"",     ""layout"": ""Vector3"", ""synthetic"": true, ""offset"": ""64"" },
        {""name"": ""Accelerometer"", ""layout"": ""Vector3"", ""synthetic"": true }
        ]
        }";

        static AddGamepadWithIMU()
        {
            InputSystem.RegisterLayoutOverride(GamepadWithIMUOverride);
        }
    }
}
#endif
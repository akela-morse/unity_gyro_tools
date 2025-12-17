using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Assertions;

public class GyroRotatedObject : MonoBehaviour
{
    RotationInputs rotationInputs;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rotationInputs = new RotationInputs();
        rotationInputs.Imu.Enable();
        rotationInputs.Imu.Gyro.performed += GyroToRotation;
        
    }

    void GyroToRotation(InputAction.CallbackContext context) => GyroToRotation(SafelyReadCallbackValue<Vector3>(context));

    void GyroToRotation(Vector3 gyroscope)
    {
        if(Mathf.Abs(gyroscope.x) > 0.01f && Mathf.Abs(gyroscope.y) > 0.01f)
        {
            /// According to the SDL wiki SDL uses a right hand coordinate system where Y is up
            /// Thus positive rotations are those seen from the positive side of an axis going counter clockwise
            /// 
            /// Unity uses a left hand coordinate system where Y is up
            /// Thus positive rotations are those seen from the positive side of an axis going clockwise
            /// 
            /// Thus we translate the values from SDL to be in line with the Unity standard

            //gyroscope.x *= -1;
            //gyroscope.y *= -1;

           
            gameObject.transform.Rotate(gyroscope);

        }
        
    }

    /// <summary>
    /// Method to bypass potential type unsafety when using input action callbacks
    /// </summary>
    /// <typeparam name="T">Type you want to extract from the context</typeparam>
    /// <param name="context">Callback context parameter from input action callback</param>
    /// <returns></returns>
    static T SafelyReadCallbackValue<T>(InputAction.CallbackContext context) where T : struct
    {
        Assert.IsTrue(
            context.valueType == typeof(T),
            $"InputAction '{context.action.name}' expected value type {typeof(T).Name}, " +
            $"but was {context.valueType?.Name ?? "null"}"
        );

        return context.ReadValue<T>();
    }
}

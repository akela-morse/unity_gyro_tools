using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Assertions;
using System.Collections;
using System.Collections.Generic;
using System;
using MoreStories.GyroTools;

public class GyroRotatedObject : MonoBehaviour
{
    RotationInputs rotationInputs;


    #region Input Action Helpers

    // Showing an example where you might want to have several input records for each action so as to use a delta time variable
    // Usually this would be overkill and it'd be best to just make individual variables
    // Purely for illustrative purposes even though there's only one action to worry about here
    Dictionary<InputAction, double> lastInputContextInvokeTimes = new Dictionary<InputAction, double>();
    
    /// <summary>
    /// Method to bypass potential type unsafety when using input action callbacks
    /// </summary>
    /// <typeparam name="T">Type you want to extract from the context</typeparam>
    /// <param name="context">Callback context parameter from input action callback</param>
    /// <returns></returns>
    T SafelyReadCallbackValue<T>(InputAction.CallbackContext context) where T : struct
    {
        Assert.IsTrue(
            context.valueType == typeof(T),
            $"InputAction '{context.action.name}' expected value type {typeof(T).Name}, " +
            $"but was {context.valueType?.Name ?? "null"}"
        );

        return context.ReadValue<T>();
    }

    void RegisterInputCallback(InputAction inputAction, Action<InputAction.CallbackContext> invokedMethod)
    {
        lastInputContextInvokeTimes.TryAdd(inputAction, 0);
        inputAction.performed += invokedMethod;
    }

    #endregion

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //var a = GyroOverride.hil;
        rotationInputs = new RotationInputs();
        rotationInputs.Imu.Enable();
        
        rotationInputs.Imu.DisableGyro.performed += ToggleGyroscope;
        rotationInputs.Imu.SizeChange.performed += ChangeScale;
        rotationInputs.Imu.Quit.performed += (x) => Application.Quit();
        RegisterInputCallback(rotationInputs.Imu.Motion, GyroToRotation);
        
    }

    void ChangeScale(InputAction.CallbackContext context)
    {
        float x = SafelyReadCallbackValue<float>(context);
        transform.localScale =  Vector3.one * (1 + 0.5f*x);
    }
    void ToggleGyroscope(InputAction.CallbackContext context)
    {
        
        if(rotationInputs.Imu.Motion.enabled)
        {
             rotationInputs.Imu.Motion.Disable();
        }
        else
        {
            rotationInputs.Imu.Motion.Enable();
        }
    }

    void GyroToRotation(InputAction.CallbackContext context)
    {
        IMUState imu = SafelyReadCallbackValue<IMUState>(context);
        Vector3 gyroscope = imu.gyroscope;
        Debug.Log(gyroscope);
        
        if(Mathf.Abs(gyroscope.x) > 0.01f && Mathf.Abs(gyroscope.y) > 0.01f)
        {
            float deltaTime = (float)(context.time - lastInputContextInvokeTimes[context.action]);
            lastInputContextInvokeTimes[context.action] = context.time;
            gameObject.transform.Rotate(gyroscope * Mathf.Rad2Deg * deltaTime);
        }
    }

    

}

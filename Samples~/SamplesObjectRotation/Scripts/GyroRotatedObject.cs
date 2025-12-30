using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Assertions;
using System.Collections;
using System.Collections.Generic;
using System;

public class GyroRotatedObject : MonoBehaviour
{
    const float InputUpdateDelayMS = 0.001f;
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
        rotationInputs = new RotationInputs();
        rotationInputs.Imu.Enable();
        RegisterInputCallback(rotationInputs.Imu.Gyro, GyroToRotation);

        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        
        StartCoroutine(UpdateInputSystem(new WaitForSeconds(InputUpdateDelayMS)));
        Application.quitting += OnQuit;
        
    }

    IEnumerator UpdateInputSystem(WaitForSeconds delay)
    {
        while(true)
        {  
            InputSystem.Update();       
            yield return delay;
        }
    }

    void OnQuit()
    {
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
    }



    void GyroToRotation(InputAction.CallbackContext context)
    {
        Vector3 gyroscope = SafelyReadCallbackValue<Vector3>(context);
        
        if(Mathf.Abs(gyroscope.x) > 0.01f && Mathf.Abs(gyroscope.y) > 0.01f)
        {
            float deltaTime = (float)(context.time - lastInputContextInvokeTimes[context.action]);
            lastInputContextInvokeTimes[context.action] = context.time;

            gameObject.transform.Rotate(gyroscope * Mathf.Rad2Deg * deltaTime);
        }
    }

    

}
